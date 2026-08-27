# Codex 작업 지침

코드를 수정하기 전에 [`SYSTEM_ARCHITECTURE.md`](SYSTEM_ARCHITECTURE.md)를 읽고 관련 클래스, 데이터 관계, API 계약의 영향 범위를 확인한다.

- 구조, 클래스 책임, endpoint, entity 관계 또는 설정이 바뀌면 `SYSTEM_ARCHITECTURE.md`도 함께 갱신한다.
- API 사용법이 바뀌면 `README.md`와 `sweeper-server.http`도 함께 확인한다.
- entity 또는 `SweeperDbContext`가 바뀌면 새 EF Core migration 필요 여부를 확인한다.
- 비밀번호, 연결 문자열, JWT signing key, OAuth credential 같은 비밀값을 새로 커밋하지 않는다.
- 완료 전 최소한 `dotnet build`를 실행하고 변경된 흐름을 검증한다.
