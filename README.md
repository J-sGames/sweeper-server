# Sweeper Server

게임 플레이 결과를 저장하는 ASP.NET Core Web API입니다. Entity Framework Core와 MySQL을 사용합니다.

## 프로젝트 소개

Sweeper Server는 게임 클라이언트에서 전달받은 플레이 결과를 검증하고 MySQL에 저장하는 백엔드 API 서버입니다. 플레이어 이름, 점수, 플레이 시작 시각과 종료 시각을 기록하며, 저장 결과를 공통 형식의 JSON 응답으로 반환합니다.

### 주요 기능

- 게임 플레이 결과 등록
- 플레이어 이름 유효성 검사
- 플레이 시작·종료 시각 검증
- Entity Framework Core를 통한 MySQL 데이터 저장
- 성공 여부와 오류 코드를 포함한 일관된 API 응답 제공

## 기술 스택

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core 9
- MySQL
- Pomelo Entity Framework Core MySQL Provider

## 사전 준비

개발 환경에 다음 도구가 필요합니다.

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- MySQL 8 이상
- EF Core CLI 도구

EF Core CLI가 없다면 설치합니다.

```bash
dotnet tool install --global dotnet-ef --version 9.*
```

이미 설치했다면 프로젝트 버전에 맞게 업데이트할 수 있습니다.

```bash
dotnet tool update --global dotnet-ef --version 9.*
```

## 1. 프로젝트 내려받기 및 패키지 복원

```bash
git clone <repository-url>
cd sweeper-server
dotnet restore
```

## 2. MySQL 데이터베이스 준비

MySQL에 관리자 계정으로 접속한 뒤 데이터베이스와 개발용 계정을 생성합니다. 아래 비밀번호는 예시이므로 실제 개발 환경에 맞게 변경하세요.

```sql
CREATE DATABASE sweeper_server
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

CREATE USER 'sweeper_user'@'localhost'
    IDENTIFIED BY 'change_me';

GRANT ALL PRIVILEGES ON sweeper_server.*
    TO 'sweeper_user'@'localhost';

FLUSH PRIVILEGES;
```

## 3. 연결 문자열 설정

비밀번호가 저장소에 남지 않도록 환경 변수나 .NET User Secrets 사용을 권장합니다.

### User Secrets 사용

최초 한 번 User Secrets를 초기화하고 연결 문자열을 등록합니다.

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:SweeperDB" "server=localhost;port=3306;database=sweeper_server;user=sweeper_user;password=change_me"
```

### 환경 변수 사용

PowerShell:

```powershell
$env:ConnectionStrings__SweeperDB = "server=localhost;port=3306;database=sweeper_server;user=sweeper_user;password=change_me"
```

bash/zsh:

```bash
export ConnectionStrings__SweeperDB="server=localhost;port=3306;database=sweeper_server;user=sweeper_user;password=change_me"
```

환경 변수의 이중 밑줄(`__`)은 설정 키의 구분자인 `:`에 해당합니다.

## 4. 데이터베이스 마이그레이션 적용

프로젝트 루트에서 기존 마이그레이션을 적용합니다.

```bash
dotnet ef database update
```

모델을 변경한 경우 새 마이그레이션을 만든 뒤 적용합니다.

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## 5. 서버 실행

```bash
dotnet run
```

개발 프로필의 기본 주소는 다음과 같습니다.

- HTTP: `http://localhost:5065`
- HTTPS: `https://localhost:7097`

HTTPS 개발 인증서를 신뢰해야 한다면 다음 명령을 실행합니다.

```bash
dotnet dev-certs https --trust
```

## API 확인

플레이 결과 저장:

```http
POST /api/result/achieve
Content-Type: application/json
```

요청 예시:

```json
{
  "name": "player1",
  "score": 1200,
  "startedTime": "2026-08-19T10:00:00Z",
  "endedTime": "2026-08-19T10:05:00Z"
}
```

`curl`로 확인하는 예시:

```bash
curl -X POST "http://localhost:5065/api/result/achieve" \
  -H "Content-Type: application/json" \
  -d '{"name":"player1","score":1200,"startedTime":"2026-08-19T10:00:00Z","endedTime":"2026-08-19T10:05:00Z"}'
```

성공 응답 예시:

```json
{
  "success": true,
  "data": {
    "id": 1,
    "rank": 1,
    "name": "player1",
    "score": 1200
  },
  "errorCode": null
}
```

### API 응답 요약

모든 응답은 다음 공통 형식을 사용합니다.

| 필드 | 타입 | 설명 |
| --- | --- | --- |
| `success` | `boolean` | 요청 처리 성공 여부 |
| `data` | `object \| null` | 성공 시 저장된 결과 데이터 |
| `errorCode` | `string \| null` | 실패 원인을 나타내는 오류 코드 |

성공하면 HTTP `200 OK`와 함께 저장된 플레이 결과를 반환합니다.

| `data` 필드 | 타입 | 설명 |
| --- | --- | --- |
| `id` | `number` | 생성된 플레이 기록 ID |
| `rank` | `number` | 등록 직후 전체 랭킹 기준 등수 |
| `name` | `string` | 공백을 제거한 플레이어 이름 |
| `score` | `number` | 저장된 점수 |

입력값 검증에 실패하면 HTTP `400 Bad Request`를 반환합니다.

| 오류 코드 | 발생 조건 |
| --- | --- |
| `INVALID_NAME` | 플레이어 이름이 비어 있거나 공백으로만 구성된 경우 |
| `INVALID_PLAYTIME` | 종료 시각이 시작 시각보다 빠른 경우 |

실패 응답 예시:

```json
{
  "success": false,
  "data": null,
  "errorCode": "INVALID_NAME"
}
```

## 빌드 확인

```bash
dotnet build
```

## 인증 설정

JWT 서명 키와 Google OAuth 클라이언트 ID는 저장소에 넣지 말고 User Secrets로 설정합니다.

```bash
dotnet user-secrets set "Jwt:Key" "32바이트-이상의-충분히-긴-임의의-비밀키"
dotnet user-secrets set "Google:ClientId" "Google-Cloud에서-발급한-클라이언트-ID"
```

인증 테이블을 데이터베이스에 반영합니다.

```bash
dotnet ef database update
```

### API 라우팅 경로

| Method | 경로 | 설명 |
| --- | --- | --- |
| `POST` | `/api/auth/register` | ID/비밀번호 회원가입 및 로그인 |
| `POST` | `/api/auth/login` | ID/비밀번호 로그인 |
| `POST` | `/api/auth/google` | Google ID Token 로그인 및 최초 자동 가입 |
| `POST` | `/api/auth/refresh` | Access/Refresh Token 갱신 |
| `POST` | `/api/auth/logout` | Refresh Token 폐기 |
| `GET` | `/api/auth/me` | 현재 사용자 조회 (`Bearer` 인증 필요) |
| `POST` | `/api/result/achieve` | 플레이 결과 등록 |
| `GET` | `/api/result/ranking?page={page}&pageSize={pageSize}` | 페이지 단위 랭킹 조회 (`{page}`, `{pageSize}`에 원하는 값 입력) |

자체 회원가입 요청 예시:

```json
{
  "loginId": "player01",
  "password": "Example1234!",
  "nickname": "플레이어"
}
```

Google 로그인 요청에서는 클라이언트의 Google SDK가 발급한 ID Token을 전달합니다.

```json
{
  "idToken": "google-id-token",
  "nickname": "플레이어"
}
```

로그인 성공 후 보호된 API에는 다음 헤더를 사용합니다.

```http
Authorization: Bearer <accessToken>
```

빌드 결과물은 기본적으로 `bin/Debug/net10.0`에 생성됩니다.

## 주요 폴더

```text
Controllers/   API 엔드포인트
Datas/         EF Core DbContext
Dtos/          요청 및 응답 DTO
Migrations/    데이터베이스 마이그레이션
Models/        데이터 모델
Responses/     공통 API 응답 형식
Services/      비즈니스 로직
```

## 문제 해결

- **MySQL 연결 실패**: MySQL 실행 여부, 포트 `3306`, 계정 권한과 연결 문자열을 확인합니다.
- **`dotnet ef`를 찾을 수 없음**: EF Core CLI를 설치한 뒤 터미널을 다시 엽니다.
- **HTTPS 인증서 오류**: `dotnet dev-certs https --trust`를 실행하거나 HTTP 주소를 사용합니다.
- **마이그레이션 버전 문제**: `dotnet-ef` 도구와 EF Core 패키지가 모두 9.x인지 확인합니다.
