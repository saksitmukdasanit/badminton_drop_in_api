
```
DropInBadAPI
├─ Data
│  └─ BadmintonDbContext.cs
├─ DropInBadAPI.csproj
├─ DropInBadAPI.http
├─ DropInBadAPI.sln
├─ Models
│  ├─ Bank.cs
│  ├─ BillLineItem.cs
│  ├─ Facility.cs
│  ├─ GameSession.cs
│  ├─ GameSessionFacility.cs
│  ├─ GameSessionPhoto.cs
│  ├─ GameType.cs
│  ├─ Match.cs
│  ├─ MatchPlayer.cs
│  ├─ Notification.cs
│  ├─ OrganizerProfile.cs
│  ├─ OrganizerSkillLevel.cs
│  ├─ PairingMethod.cs
│  ├─ ParticipantBill.cs
│  ├─ Payment.cs
│  ├─ SessionParticipant.cs
│  ├─ SessionWalkinGuest.cs
│  ├─ ShuttlecockBrand.cs
│  ├─ ShuttlecockModel.cs
│  ├─ SkillLevel.cs
│  ├─ User.cs
│  ├─ UserBookmarkedSession.cs
│  ├─ UserFcmToken.cs
│  ├─ UserFollow.cs
│  ├─ UserLogin.cs
│  ├─ UserOrganizerSkill.cs
│  ├─ UserProfile.cs
│  ├─ UserWallet.cs
│  ├─ Venue.cs
│  └─ WalletTransaction.cs
├─ Modules
│  ├─ Auth
│  │  ├─ AuthController.cs
│  │  ├─ AuthDtos.cs
│  │  ├─ AuthService.cs
│  │  ├─ IAuthService.cs
│  │  ├─ IJwtService.cs
│  │  └─ JwtService.cs
│  ├─ Cms
│  ├─ Common
│  │  ├─ ApiResponse.cs
│  │  ├─ Dropdown
│  │  │  ├─ DropdownController.cs
│  │  │  ├─ DropdownDto.cs
│  │  │  ├─ DropdownService.cs
│  │  │  └─ IDropdownService.cs
│  │  └─ ParticipantTypes.cs
│  ├─ Master
│  │  ├─ BanksController.cs
│  │  ├─ FacilitiesController.cs
│  │  ├─ GameTypesController.cs
│  │  ├─ GenericService.cs
│  │  ├─ IGenericService.cs
│  │  ├─ IMasterDataEntity.cs
│  │  ├─ PairingMethodsController.cs
│  │  ├─ ShuttlecockBrandsController.cs
│  │  └─ ShuttlecockModelsController.cs
│  ├─ MobileOrganizer
│  │  ├─ DashBoard
│  │  │  ├─ IOrganizerDashboardService.cs
│  │  │  ├─ OrganizerDashboardController.cs
│  │  │  ├─ OrganizerDashboardDto.cs
│  │  │  └─ OrganizerDashboardService.cs
│  │  ├─ Game
│  │  │  ├─ GameSessionAnalyticsDto.cs
│  │  │  ├─ GameSessionDtos.cs
│  │  │  ├─ GameSessionFinancialsDto.cs
│  │  │  ├─ GameSessionService.cs
│  │  │  ├─ GameSessionsController.cs
│  │  │  └─ IGameSessionService.cs
│  │  ├─ MatchManagement
│  │  │  ├─ IMatchManagementService.cs
│  │  │  ├─ IMatchRecommenderService.cs
│  │  │  ├─ ManageDtos.cs
│  │  │  ├─ ManagementGaneHub.cs
│  │  │  ├─ MatchManagementController.cs
│  │  │  ├─ MatchManagementService.cs
│  │  │  └─ MatchRecommenderService.cs
│  │  └─ Organizer
│  │     ├─ IOrganizerService.cs
│  │     ├─ OrganizerController.cs
│  │     ├─ OrganizerDtos.cs
│  │     ├─ OrganizerService.cs
│  │     └─ SkillLevel
│  │        ├─ IOrganizerSkillLevelService.cs
│  │        ├─ OrganizerSkillLevelDtos.cs
│  │        ├─ OrganizerSkillLevelService.cs
│  │        └─ OrganizerSkillLevelsController.cs
│  ├─ MobilePlayer
│  │  ├─ Dashboard
│  │  │  ├─ IPlayerDashboardService.cs
│  │  │  ├─ PlayerDashboardController.cs
│  │  │  ├─ PlayerDashboardDto.cs
│  │  │  └─ PlayerDashboardService.cs
│  │  ├─ Follows
│  │  │  ├─ FollowService.cs
│  │  │  ├─ FollowsController.cs
│  │  │  └─ IFollowService.cs
│  │  ├─ PlayerGameSession
│  │  │  ├─ IPlayerGameSessionService.cs
│  │  │  ├─ PlayerGameSessionDtos.cs
│  │  │  ├─ PlayerGameSessionService.cs
│  │  │  └─ PlayerGameSessionsController.cs
│  │  ├─ PlayerMatch
│  │  │  ├─ IPlayerMatchService.cs
│  │  │  ├─ PlayerMatchDtos.cs
│  │  │  ├─ PlayerMatchService.cs
│  │  │  └─ PlayerMatchesController.cs
│  │  ├─ Profile
│  │  │  ├─ IProfileService.cs
│  │  │  ├─ ProfileDtos.cs
│  │  │  ├─ ProfileService.cs
│  │  │  └─ ProfilesController.cs
│  │  └─ Wallet
│  │     ├─ IWalletService.cs
│  │     ├─ WalletController.cs
│  │     ├─ WalletDtos.cs
│  │     └─ WalletService.cs
│  ├─ Notification
│  │  ├─ INotificationService.cs
│  │  ├─ NotificationDto.cs
│  │  ├─ NotificationService.cs
│  │  └─ NotificationsController.cs
│  ├─ Shared
│  │  └─ SharedDtos.cs
│  └─ Webhooks
│     └─ Xendit
│        ├─ IXenditService.cs
│        ├─ XenditService.cs
│        └─ XenditWebhookController.cs
├─ Program.cs
├─ Properties
│  └─ launchSettings.json
├─ Utility
│  ├─ Combinatorics.cs
│  └─ Helper.cs
├─ appsettings.json
├─ context.md
└─ firebase-adminsdk.json

```