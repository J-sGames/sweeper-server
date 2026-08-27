# 클라이언트 랭킹 기능 변경 요구사항

## 목적

클라이언트에 전체 플레이 기록을 한 번에 내려받지 않는 페이지 단위 랭킹 화면을 추가한다.

첫 페이지는 1~10위, 두 번째 페이지는 11~20위처럼 조회하며 사용자가 다음 페이지로 이동하거나 추가 목록을 불러올 수 있어야 한다.

## 서버 API 계약

### 요청

```http
GET /api/result/ranking?page={page}&pageSize={pageSize}
Accept: application/json
```

| query | 타입 | 기본값 | 허용 범위 | 설명 |
| --- | --- | --- | --- | --- |
| `page` | integer | `1` | 1 이상 | 1부터 시작하는 페이지 번호 |
| `pageSize` | integer | `10` | 1~100 | 한 페이지에 표시할 기록 수 |

호출 예시:

```http
# 1~10위
GET /api/result/ranking?page=1&pageSize=10

# 11~20위
GET /api/result/ranking?page=2&pageSize=10
```

현재 이 endpoint에는 Bearer token이 필요하지 않다.

### 성공 응답

```json
{
  "success": true,
  "data": {
    "page": 2,
    "pageSize": 10,
    "hasNext": true,
    "items": [
      {
        "rank": 11,
        "name": "player11",
        "score": 900,
        "achievedAt": "2026-08-20T10:05:00Z"
      }
    ]
  },
  "errorCode": null
}
```

| 응답 필드 | 타입 | 설명 |
| --- | --- | --- |
| `data.page` | integer | 서버가 반환한 현재 페이지 |
| `data.pageSize` | integer | 요청한 페이지 크기 |
| `data.hasNext` | boolean | 다음 페이지가 존재하는지 여부 |
| `data.items` | array | 현재 페이지의 랭킹 목록. 결과가 없으면 빈 배열 |
| `items[].rank` | integer | 전체 랭킹 기준 순위. 두 번째 10개 페이지는 11부터 시작 |
| `items[].name` | string | 플레이 기록에 저장된 플레이어 이름 |
| `items[].score` | integer | 점수 |
| `items[].achievedAt` | ISO 8601 datetime | 플레이 종료 시각 |

클라이언트에서 배열 index로 순위를 다시 계산하지 말고 서버가 반환한 `rank`를 표시한다.

### 실패 응답

잘못된 query는 HTTP `400 Bad Request`로 반환된다.

| `errorCode` | 조건 | 클라이언트 처리 |
| --- | --- | --- |
| `INVALID_RANKING_PAGE` | `page`가 1보다 작거나 계산 가능한 범위를 벗어남 | 첫 페이지로 복귀 후 재요청 |
| `INVALID_RANKING_PAGE_SIZE` | `pageSize`가 1~100 범위를 벗어남 | 기본값 10으로 재요청 |

네트워크 오류나 서버 오류는 query 오류와 구분하여 재시도 UI를 제공한다.

## 클라이언트 구현 요구사항

### 데이터 모델

다음 구조에 대응하는 클라이언트 모델을 추가한다.

```text
RankingPage
├─ page: number
├─ pageSize: number
├─ hasNext: boolean
└─ items: RankingItem[]

RankingItem
├─ rank: number
├─ name: string
├─ score: number
└─ achievedAt: datetime/string
```

공통 API envelope의 `success`, `data`, `errorCode` 처리 규칙은 기존 API와 동일하게 사용한다.

### 화면과 상태

- 최초 진입 시 `page=1&pageSize=10`을 요청한다.
- 각 행에는 최소한 순위, 플레이어 이름, 점수를 표시한다.
- `achievedAt`을 표시한다면 사용자 locale에 맞게 변환한다.
- 요청 중에는 중복 요청을 방지하고 loading 상태를 표시한다.
- `items`가 빈 배열이면 오류가 아니라 빈 랭킹 상태를 표시한다.
- `hasNext=false`이면 다음 페이지 버튼 또는 더 보기 동작을 비활성화한다.
- 이전/다음 페이지 방식이라면 현재 `page`를 화면 상태로 유지한다.
- 무한 스크롤/더 보기 방식이라면 새 `items`를 기존 목록 뒤에 추가하고 중복 호출을 막는다.
- 새로고침할 때는 페이지와 목록을 초기화한 뒤 1페이지부터 다시 요청한다.

### 정렬 규칙

정렬은 서버가 결정하므로 클라이언트에서 목록을 다시 정렬하지 않는다.

서버 정렬 기준은 다음과 같다.

1. 점수 높은 순
2. 동점이면 플레이 종료 시각이 빠른 순
3. 종료 시각도 같으면 기록 ID가 작은 순

## 완료 조건

- 첫 요청에서 1~10위가 표시된다.
- 다음 페이지 요청에서 11~20위가 표시되고 `rank`가 11부터 시작한다.
- 마지막 페이지에서 다음 페이지 동작이 비활성화된다.
- 기록이 10개 미만이거나 없는 경우에도 정상적으로 표시된다.
- loading, 빈 목록, API 오류, 네트워크 오류 상태가 서로 구분된다.
- 빠른 연속 조작으로 같은 페이지가 중복 추가되지 않는다.
- 화면을 다시 열거나 새로고침하면 1페이지부터 일관되게 로드된다.

## 서버 구현 참고

서버는 DB query에 `Skip((page - 1) * pageSize)`와 `Take(pageSize + 1)`을 적용한다. 전체 랭킹 데이터를 먼저 불러온 뒤 자르는 방식이 아니며, 추가 1건은 `hasNext` 판단에만 사용된다.
