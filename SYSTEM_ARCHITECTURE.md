# Sweeper Server 시스템 구조

> 이 문서는 Codex와 개발자가 코드 변경 전 영향 범위를 빠르게 파악하기 위한 기준 문서다.
> 구조나 책임이 바뀌면 관련 코드와 함께 이 문서도 갱신한다.

## 1. 시스템 개요

Sweeper Server는 게임 플레이 결과와 사용자 인증을 처리하는 ASP.NET Core Web API다.

- 런타임: .NET 10 / ASP.NET Core
- 데이터 접근: Entity Framework Core 9
- 데이터베이스: MySQL 8, Pomelo provider
- 인증: JWT Bearer access token + DB에 해시로 저장하는 refresh token
- 외부 로그인: Google ID token 검증
- 기본 처리 흐름: `HTTP 요청 → Controller → Service → SweeperDbContext → MySQL`

프로젝트는 별도의 도메인 계층이나 repository 계층 없이 Service가 EF Core의 `SweeperDbContext`를 직접 사용한다.

## 2. 디렉터리 구조

| 위치 | 역할 | 변경 시 확인할 곳 |
| --- | --- | --- |
| `Program.cs` | 애플리케이션 시작점, DI, DB, JWT 인증 middleware 구성 | `appsettings*.json`, `Services/JwtOptions.cs` |
| `Controllers/` | HTTP route와 status code 결정 | 대응하는 `Dtos/`, `Services/`, `Responses/` |
| `Services/` | 입력 검증, 인증, 토큰 발급, 저장 등 업무 로직 | `Models/`, `Datas/SweeperDbContext.cs` |
| `Datas/` | EF Core DbContext와 테이블 관계/제약 정의 | `Models/`, `Migrations/` |
| `Models/` | DB entity와 navigation property | `Datas/SweeperDbContext.cs`, `Migrations/` |
| `Dtos/` | API 요청/응답 계약 | `Controllers/`, 클라이언트 호환성 |
| `Responses/` | 공통 API 응답 envelope | 모든 Controller와 Service |
| `Migrations/` | DB schema 변경 이력과 model snapshot | entity/DbContext 변경 시 함께 생성 |
| `Properties/launchSettings.json` | 로컬 실행 profile과 URL | 로컬 실행 방식 변경 시 |
| `sweeper-server.http` | API 수동 호출 예제 | endpoint 계약 변경 시 |
| `sweeper-server.csproj` | target framework와 NuGet 의존성 | 패키지 및 framework 변경 시 |

`bin/`, `obj/`, `.dotnet-cli/`는 생성물 또는 로컬 도구 영역이므로 기능 수정 대상으로 보지 않는다.

## 3. 시작점과 공통 구성

### `Program.cs`

애플리케이션 전체 조립을 담당하는 top-level 진입점이다.

- `ConnectionStrings:SweeperDB`를 읽어 `SweeperDbContext`를 MySQL에 연결한다.
- `PlayLogService`, `AuthService`, `TokenService`를 scoped로 등록한다.
- `PasswordHasher<User>`를 singleton으로 등록한다.
- `Jwt` 설정을 `JwtOptions`에 binding한다.
- JWT signing key가 UTF-8 기준 32바이트 미만이면 시작을 중단한다.
- JWT issuer, audience, lifetime, signing key를 검증한다.
- middleware 순서는 인증 → 인가 → controller mapping이다.

서비스나 controller를 추가하면 이 파일의 DI 등록 필요 여부를 먼저 확인한다.

### 설정 파일

| 위치/키 | 용도 |
| --- | --- |
| `appsettings.json` / `ConnectionStrings:SweeperDB` | MySQL 연결 문자열 |
| `appsettings.json` / `Jwt:*` | issuer, audience, signing key, access/refresh 만료 기간 |
| `appsettings.json` / `Google:ClientId` | Google ID token audience 검증 값 |
| `appsettings.Development.json` | 개발 환경 logging override |

비밀번호, JWT signing key, Google client ID 같은 실제 비밀값은 커밋하지 않고 User Secrets 또는 환경 변수로 주입한다.

## 4. HTTP/API 계층

### `Controllers/AuthController.cs` — `AuthController`

기본 route는 `/api/auth`이며 인증 관련 HTTP 계약과 응답 status를 담당한다.

| 메서드 | endpoint | 위임 대상 | 주요 응답 |
| --- | --- | --- | --- |
| `Register` | `POST /api/auth/register` | `AuthService.RegisterAsync` | 200, 중복 시 409 |
| `Login` | `POST /api/auth/login` | `AuthService.LoginAsync` | 200, 실패 시 401 |
| `Google` | `POST /api/auth/google` | `AuthService.GoogleLoginAsync` | 200, 미설정 시 503, 실패 시 401 |
| `Refresh` | `POST /api/auth/refresh` | `AuthService.RefreshAsync` | 200 또는 401 |
| `Logout` | `POST /api/auth/logout` | `AuthService.LogoutAsync` | 항상 성공 envelope 반환 |
| `Me` | `GET /api/auth/me` | `AuthService.GetUserAsync` | JWT 필요, 200/401/404 |

`ToActionResult`가 service의 문자열 오류 코드를 HTTP status로 변환한다. 인증 오류 코드를 추가할 때 이 mapping도 함께 수정한다.

### `Controllers/LogController.cs` — `LogController`

- `POST /api/result/achieve`: `PlayLogRequest`를 받아 `PlayLogService.InsertPlayLogAsync`에 위임한다.
- `GET /api/result/ranking?page=1&pageSize=10`: 요청한 페이지의 기록만 조회하며 `PlayLogService.GetRankingAsync`에 위임한다.
- service 결과가 성공이면 200, 입력값이 잘못되면 400을 반환한다.
- action 이름은 현재 `Login`이지만 실제 책임은 플레이 기록 저장이다.
- 현재 `[Authorize]`가 없어 인증 없이 호출할 수 있으며, `PlayLog`도 `User`와 연결되어 있지 않다.

## 5. 서비스 계층

### `Services/AuthService.cs` — `AuthService`

로컬/Google 인증, refresh token 회전, 사용자 조회를 담당한다.

| 메서드 | 책임 |
| --- | --- |
| `RegisterAsync` | login ID 정규화, login ID/닉네임 중복 검사, 비밀번호 hash, 사용자 저장, token 발급 |
| `LoginAsync` | credential 조회와 비밀번호 검증 후 token 발급 |
| `GoogleLoginAsync` | Google ID token 검증, 기존 외부 계정 로그인 또는 신규 사용자 생성 |
| `RefreshAsync` | refresh token hash 조회 및 유효성 검사, 기존 token 폐기, 새 token 발급 |
| `LogoutAsync` | 일치하는 refresh token을 폐기 처리 |
| `GetUserAsync` | 사용자와 인증 provider 정보를 조회해 `UserResponse`로 변환 |
| `IssueTokensAsync` | access/refresh token 생성, refresh token entity 저장 |
| `CreateAvailableNicknameAsync` | Google 가입용 최대 20자 고유 닉네임 생성 |
| `NormalizeLoginId` | trim 후 대문자로 바꿔 login ID 비교 기준 통일 |

반환 형식은 `(AuthResponse? Data, string? Error)` tuple이다. 트랜잭션 경계는 별도로 선언하지 않으며 각 흐름에서 `SaveChangesAsync`를 호출한다.

### `Services/TokenService.cs` — `TokenService`

- `CreateAccessToken`: 사용자 ID(`sub`), 닉네임(`unique_name`), JWT ID(`jti`) claim으로 HMAC-SHA256 JWT를 만든다.
- `CreateRefreshToken`: 암호학적 난수 64바이트를 Base64 문자열로 생성하고, 원문은 응답용으로만 반환한다.
- `Hash`: refresh token을 SHA-256 대문자 hex 문자열로 변환한다.
- DB에는 refresh token 원문이 아니라 `TokenHash`만 저장된다.

### `Services/PlayLogService.cs` — `PlayLogService`

- `InsertPlayLogAsync`: 이름 공백 여부와 시작/종료 시간 순서를 검사한다.
- 성공 시 `PlayLog`를 저장하고 랭킹과 동일한 정렬 기준으로 등록 직후 등수를 계산해 ID, 등수, 이름, 점수가 포함된 `PlayLogResponse`를 반환한다.
- 오류 코드는 `INVALID_NAME`, `INVALID_PLAYTIME`이다.
- 점수 범위, 날짜의 UTC 여부, 사용자 소유권은 현재 검증하지 않는다.
- `GetRankingAsync`: `page`는 1 이상, `pageSize`는 1~100으로 제한하고 점수 내림차순, 종료 시각, ID 순으로 정렬한다.
- DB query에 `Skip((page - 1) * pageSize)`와 `Take(pageSize + 1)`을 적용한다. 추가 1건은 `hasNext` 판단에만 사용한다.
- 랭킹 조회는 read-only이므로 `AsNoTracking`을 사용하며 잘못된 값에는 `INVALID_RANKING_PAGE` 또는 `INVALID_RANKING_PAGE_SIZE`를 반환한다.

### `Services/JwtOptions.cs` — `JwtOptions`

`Jwt` 설정 section을 나타내는 options class다. 기본 access token 만료는 15분, refresh token 만료는 30일이다.

## 6. DTO와 공통 응답

### `Dtos/AuthDtos.cs`

| 클래스/enum | 용도와 핵심 제약 |
| --- | --- |
| `RegisterRequest` | login ID 4~30자의 영문/숫자/밑줄, 비밀번호 10~128자, 닉네임 2~20자 |
| `LoginRequest` | 필수 login ID와 비밀번호 |
| `GoogleLoginRequest` | 필수 Google ID token, 선택 닉네임 2~20자 |
| `RefreshTokenRequest` | token 갱신 요청 |
| `LogoutRequest` | token 폐기 요청 |
| `AuthResponse` | 사용자, access token, refresh token, access token 만료 초 |
| `UserResponse` | 사용자 ID, 닉네임, 이메일, 인증 provider 목록 |
| `AuthProvider` | `Local`, `Google`; 현재 실행 로직은 enum 대신 문자열을 사용함 |

`[ApiController]`가 DataAnnotations 검증 실패를 controller action 진입 전에 자동으로 400 처리한다.

### 플레이 기록 DTO

- `Dtos/PlayLogRequest.cs` — `PlayLogRequest`: 이름, 점수, 시작 시각, 종료 시각 입력.
- `Dtos/PlayLogResponse.cs` — `PlayLogResponse`: 생성 ID, 등록 직후 전체 등수, 이름, 점수 출력.
- `Dtos/RankingResponse.cs` — `RankingPageResponse`: 현재 페이지, 페이지 크기, 다음 페이지 여부, 랭킹 항목 목록 출력. `RankingResponse`: 전체 기준 순위, 이름, 점수, 달성 시각 출력.

### `Responses/ApiResponses.cs` — `ApiResponse<T>`

모든 업무 응답의 공통 envelope다.

- `Success`: 처리 성공 여부
- `Data`: 성공 데이터 또는 `null`
- `ErrorCode`: 실패 원인 문자열 또는 `null`

## 7. 데이터 모델과 관계

### `Datas/SweeperDbContext.cs` — `SweeperDbContext`

모든 `DbSet`과 schema 제약을 정의한다.

```text
User (1) ─── (0..1) UserCredential
  │
  ├──── (0..N) ExternalLogin
  └──── (0..N) RefreshToken

PlayLog  (현재 User와 관계 없음)
```

사용자 하위 entity는 `User` 삭제 시 cascade 삭제된다.

### Entity 위치

| 위치/클래스 | 주요 필드 | 제약/관계 |
| --- | --- | --- |
| `Models/User.cs` / `User` | ID, 닉네임, 이메일, 생성/수정 시각 | 닉네임 최대 20자 및 unique; credential 0..1, external login/token 0..N |
| `Models/UserCredential.cs` / `UserCredential` | UserId, 원본/정규화 login ID, password hash | UserId가 PK/FK; 정규화 ID unique; User와 1:1 |
| `Models/ExternalLogin.cs` / `ExternalLogin` | provider, provider user ID, 이메일 | `(Provider, ProviderUserId)` unique; User와 N:1 |
| `Models/RefreshToken.cs` / `RefreshToken` | token hash, 생성/만료/폐기 시각 | token hash unique; User와 N:1 |
| `Models/PlayLog.cs` / `PlayLog` | 이름, 점수, 시작/종료 시각 | 독립 entity, ID 자동 생성 |

entity의 필드, 관계, 길이 또는 index를 바꾸면 `SweeperDbContext.OnModelCreating`과 새 EF migration을 함께 갱신한다.

## 8. 마이그레이션

| 위치 | 내용 |
| --- | --- |
| `Migrations/20260817053026_InitialCreate*` | `PlayLogs` 최초 schema |
| `Migrations/20260820030727_AddAuthentication*` | 사용자, credential, 외부 로그인, refresh token schema |
| `Migrations/SweeperDbContextModelSnapshot.cs` | 현재 EF Core 모델 snapshot |

기존 migration 파일을 직접 고치기보다 모델/DbContext 수정 후 새 migration을 생성하는 것이 기본 원칙이다.

## 9. 주요 실행 흐름

### 로컬 회원가입

```text
AuthController.Register
  → AuthService.RegisterAsync
  → User + UserCredential 생성
  → PasswordHasher<User>로 hash
  → SweeperDbContext.SaveChangesAsync
  → TokenService에서 access/refresh token 생성
  → RefreshToken hash 저장
  → ApiResponse<AuthResponse>
```

### Google 로그인

```text
AuthController.Google
  → AuthService.GoogleLoginAsync
  → Google ID token의 audience/signature 검증
  → ExternalLogin 조회
  → 기존 사용자 token 발급 또는 User + ExternalLogin 생성
```

### 토큰 갱신

```text
AuthController.Refresh
  → 입력 refresh token SHA-256 hash
  → DB token 조회 및 만료/폐기 검사
  → 기존 token RevokedAt 설정
  → 새 access/refresh token 발급 및 저장
```

### 플레이 결과 저장

```text
LogController.Login
  → PlayLogService.InsertPlayLogAsync
  → 이름/시간 검증
  → PlayLog 저장
  → Score DESC, EndedTime ASC, Id ASC 기준으로 등록 기록보다 앞선 행을 계산
  → ApiResponse<PlayLogResponse>
```

### 랭킹 조회

```text
LogController.GetRanking
  → PlayLogService.GetRankingAsync
  → page 1 이상, pageSize 1~100 검증
  → DB에서 Score DESC, EndedTime ASC, Id ASC 정렬
  → Skip(offset), Take(pageSize + 1)로 제한 조회
  → ApiResponse<RankingPageResponse>
```

## 10. 변경 작업 체크리스트

### API 추가/변경

1. `Controllers/`의 route, status code, 인증 필요 여부를 정한다.
2. `Dtos/`의 요청/응답 계약과 validation을 갱신한다.
3. 업무 규칙은 `Services/`에 둔다.
4. 공통 envelope는 `ApiResponse<T>`를 유지한다.
5. `sweeper-server.http`와 `README.md` 예제를 갱신한다.

### DB 모델 변경

1. `Models/` entity를 수정한다.
2. `SweeperDbContext`의 관계, index, 길이 제약을 확인한다.
3. 새 EF Core migration과 model snapshot을 생성한다.
4. MySQL에 migration을 적용해 확인한다.

### 인증 변경

1. `AuthController`, `AuthService`, `TokenService`의 전체 흐름을 함께 확인한다.
2. 오류 코드를 추가하면 `AuthController.ToActionResult`의 HTTP mapping을 갱신한다.
3. JWT claim을 바꾸면 발급(`TokenService`)과 소비(`AuthController.Me`) 양쪽을 수정한다.
4. refresh token 원문을 로그 또는 DB에 저장하지 않는다.

### 완료 전 검증

- `dotnet build`
- 관련 endpoint 성공/실패 경로 호출
- entity 변경 시 migration 생성 및 DB 적용 확인
- 인증 변경 시 register/login/refresh/logout/me의 연속 흐름 확인
- 이 문서와 `README.md`가 변경된 코드와 일치하는지 확인

## 11. 현재 구조상 주의할 점

- `PlayLog`는 사용자와 연결되지 않고 결과 저장 endpoint도 인증이 필요하지 않다.
- refresh token은 회전되지만 token family나 reuse detection은 없다.
- logout은 존재하지 않거나 이미 폐기된 token도 성공으로 처리한다.
- `AuthProvider` enum이 선언되어 있지만 provider 저장과 응답은 문자열 기반이다.
- 전역 예외 처리 middleware, 별도 logging 정책, 자동화된 테스트 프로젝트는 현재 없다.
- 프로젝트의 EF Core 도구/핵심 패키지는 9.x이고 target framework 및 JWT Bearer 패키지는 10.x이므로 패키지 변경 시 호환성을 확인한다.
