using System;
using System.Collections.Generic;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Data;

public partial class BadmintonDbContext : DbContext
{
    public BadmintonDbContext()
    {
    }

    public BadmintonDbContext(DbContextOptions<BadmintonDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Bank> Banks { get; set; }

    public virtual DbSet<BillLineItem> BillLineItems { get; set; }

    public virtual DbSet<Facility> Facilities { get; set; }

    public virtual DbSet<GameSession> GameSessions { get; set; }

    public virtual DbSet<GameSessionFacility> GameSessionFacilities { get; set; }

    public virtual DbSet<GameSessionPhoto> GameSessionPhotos { get; set; }

    public virtual DbSet<GameType> GameTypes { get; set; }

    public virtual DbSet<Match> Matches { get; set; }

    public virtual DbSet<MatchPlayer> MatchPlayers { get; set; }

    public virtual DbSet<OrganizerProfile> OrganizerProfiles { get; set; }

    public virtual DbSet<OrganizerSkillLevel> OrganizerSkillLevels { get; set; }

    public virtual DbSet<PairingMethod> PairingMethods { get; set; }

    public virtual DbSet<ParticipantBill> ParticipantBills { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<SessionParticipant> SessionParticipants { get; set; }

    public virtual DbSet<SessionWalkinGuest> SessionWalkinGuests { get; set; }

    public virtual DbSet<ShuttlecockBrand> ShuttlecockBrands { get; set; }

    public virtual DbSet<ShuttlecockModel> ShuttlecockModels { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserLogin> UserLogins { get; set; }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    public virtual DbSet<Venue> Venues { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bank>(entity =>
        {
            entity.HasKey(e => e.BankId).HasName("PK__Banks__AA08CB33BA739F57");

            entity.Property(e => e.BankId).HasColumnName("BankID");
            entity.Property(e => e.BankCode).HasMaxLength(10);
            entity.Property(e => e.BankName).HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<BillLineItem>(entity =>
        {
            entity.HasKey(e => e.LineItemId).HasName("PK__BillLine__8A871BEE9F7AFA9A");

            entity.Property(e => e.LineItemId).HasColumnName("LineItemID");
            entity.Property(e => e.Amount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.BillId).HasColumnName("BillID");
            entity.Property(e => e.Description).HasMaxLength(255);

            entity.HasOne(d => d.Bill).WithMany(p => p.BillLineItems)
                .HasForeignKey(d => d.BillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BillLineItems_ParticipantBills");
        });

        modelBuilder.Entity<Facility>(entity =>
        {
            entity.HasKey(e => e.FacilityId).HasName("PK__Faciliti__5FB08B945D608422");

            entity.Property(e => e.FacilityId).HasColumnName("FacilityID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FacilityName).HasMaxLength(100);
            entity.Property(e => e.IconName).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PK__GameSess__C9F49270D0B540DB");

            entity.HasIndex(e => e.SessionPublicId, "UQ__GameSess__4CA1733D41319759").IsUnique();

            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.CourtFeePerPerson).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CourtNumbers).HasMaxLength(100);
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.GameTypeId).HasColumnName("GameTypeID");
            entity.Property(e => e.GroupName).HasMaxLength(255);
            entity.Property(e => e.PairingMethodId).HasColumnName("PairingMethodID");
            entity.Property(e => e.SessionPublicId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ShuttlecockCostPerUnit).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ShuttlecockFeePerPerson).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ShuttlecockModelId).HasColumnName("ShuttlecockModelID");
            entity.Property(e => e.Status).HasDefaultValue((byte)1);
            entity.Property(e => e.TotalCourtCost).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.VenueId).HasColumnName("VenueID");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.GameSessions)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameSessions_Users");

            entity.HasOne(d => d.GameType).WithMany(p => p.GameSessions)
                .HasForeignKey(d => d.GameTypeId)
                .HasConstraintName("FK_GameSessions_GameTypes");

            entity.HasOne(d => d.PairingMethod).WithMany(p => p.GameSessions)
                .HasForeignKey(d => d.PairingMethodId)
                .HasConstraintName("FK_GameSessions_PairingMethods");

            entity.HasOne(d => d.ShuttlecockModel).WithMany(p => p.GameSessions)
                .HasForeignKey(d => d.ShuttlecockModelId)
                .HasConstraintName("FK_GameSessions_ShuttlecockModels");

            entity.HasOne(d => d.Venue).WithMany(p => p.GameSessions)
                .HasForeignKey(d => d.VenueId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameSessions_Venues");
        });

        modelBuilder.Entity<GameSessionFacility>(entity =>
        {
            entity.HasKey(e => new { e.SessionId, e.FacilityId }).HasName("PK__GameSess__9C0F9AC986423A44");

            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.FacilityId).HasColumnName("FacilityID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Facility).WithMany(p => p.GameSessionFacilities)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameSessionFacilities_Facilities");

            entity.HasOne(d => d.Session).WithMany(p => p.GameSessionFacilities)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameSessionFacilities_GameSessions");
        });

        modelBuilder.Entity<GameSessionPhoto>(entity =>
        {
            entity.HasKey(e => e.PhotoId).HasName("PK__GameSess__21B7B5825001FE45");

            entity.Property(e => e.PhotoId).HasColumnName("PhotoID");
            entity.Property(e => e.Caption).HasMaxLength(255);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.PhotoUrl)
                .HasMaxLength(500)
                .HasColumnName("PhotoURL");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");

            entity.HasOne(d => d.Session).WithMany(p => p.GameSessionPhotos)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameSessionPhotos_GameSessions");
        });

        modelBuilder.Entity<GameType>(entity =>
        {
            entity.HasKey(e => e.GameTypeId).HasName("PK__GameType__B5BCC116F62DBF2A");

            entity.Property(e => e.GameTypeId).HasColumnName("GameTypeID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TypeName).HasMaxLength(100);
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(e => e.MatchId).HasName("PK__Matches__4218C837F211F0C7");

            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");

            entity.HasOne(d => d.Session).WithMany(p => p.Matches)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Matches_GameSessions");
        });

        modelBuilder.Entity<MatchPlayer>(entity =>
        {
            entity.HasKey(e => e.MatchPlayerId).HasName("PK__MatchPla__6D9FC9EB271FF26B");

            entity.Property(e => e.MatchPlayerId).HasColumnName("MatchPlayerID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.Team).HasMaxLength(1);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.WalkinId).HasColumnName("WalkinID");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchPlayers)
                .HasForeignKey(d => d.MatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchPlayers_Matches");

            entity.HasOne(d => d.User).WithMany(p => p.MatchPlayers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_MatchPlayers_Users");

            entity.HasOne(d => d.Walkin).WithMany(p => p.MatchPlayers)
                .HasForeignKey(d => d.WalkinId)
                .HasConstraintName("FK_MatchPlayers_SessionWalkinGuests");
        });

        modelBuilder.Entity<OrganizerProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Organize__1788CCAC3D4B4531");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("UserID");
            entity.Property(e => e.BankAccountNumber).HasMaxLength(50);
            entity.Property(e => e.BankAccountPhotoUrl)
                .HasMaxLength(500)
                .HasColumnName("BankAccountPhotoURL");
            entity.Property(e => e.BankId).HasColumnName("BankID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FacebookLink).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LineId)
                .HasMaxLength(100)
                .HasColumnName("LineID");
            entity.Property(e => e.NationalId)
                .HasMaxLength(255)
                .HasColumnName("NationalID");
            entity.Property(e => e.ProfilePhotoUrl)
                .HasMaxLength(500)
                .HasColumnName("ProfilePhotoURL");
            entity.Property(e => e.PublicPhoneNumber).HasMaxLength(20);

            entity.HasOne(d => d.Bank).WithMany(p => p.OrganizerProfiles)
                .HasForeignKey(d => d.BankId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizerProfiles_Banks");

            entity.HasOne(d => d.User).WithOne(p => p.OrganizerProfile)
                .HasForeignKey<OrganizerProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizerProfiles_Users");
        });

        modelBuilder.Entity<OrganizerSkillLevel>(entity =>
        {
            entity.HasKey(e => e.SkillLevelId).HasName("PK__Organize__927B2DA7D26A5FD7");

            entity.Property(e => e.SkillLevelId).HasColumnName("SkillLevelID");
            entity.Property(e => e.ColorHexCode).HasMaxLength(7);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LevelName).HasMaxLength(50);
            entity.Property(e => e.OrganizerUserId).HasColumnName("OrganizerUserID");

            entity.HasOne(d => d.OrganizerUser).WithMany(p => p.OrganizerSkillLevels)
                .HasForeignKey(d => d.OrganizerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizerSkillLevels_Users");
        });

        modelBuilder.Entity<PairingMethod>(entity =>
        {
            entity.HasKey(e => e.PairingMethodId).HasName("PK__PairingM__E25102FD728B381C");

            entity.Property(e => e.PairingMethodId).HasColumnName("PairingMethodID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MethodName).HasMaxLength(100);
        });

        modelBuilder.Entity<ParticipantBill>(entity =>
        {
            entity.HasKey(e => e.BillId).HasName("PK__Particip__11F2FC4ADF867D9B");

            entity.Property(e => e.BillId).HasColumnName("BillID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.WalkinId).HasColumnName("WalkinID");

            entity.HasOne(d => d.Session).WithMany(p => p.ParticipantBills)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ParticipantBills_GameSessions");

            entity.HasOne(d => d.User).WithMany(p => p.ParticipantBills)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_ParticipantBills_Users");

            entity.HasOne(d => d.Walkin).WithMany(p => p.ParticipantBills)
                .HasForeignKey(d => d.WalkinId)
                .HasConstraintName("FK_ParticipantBills_SessionWalkinGuests");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A585499772A");

            entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
            entity.Property(e => e.Amount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.BillId).HasColumnName("BillID");
            entity.Property(e => e.PaymentDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ReceivedByUserId).HasColumnName("ReceivedByUserID");

            entity.HasOne(d => d.Bill).WithMany(p => p.Payments)
                .HasForeignKey(d => d.BillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_ParticipantBills");
        });

        modelBuilder.Entity<SessionParticipant>(entity =>
        {
            entity.HasKey(e => e.ParticipantId).HasName("PK__SessionP__7227997E4EAF6116");

            entity.Property(e => e.ParticipantId).HasColumnName("ParticipantID");
            entity.Property(e => e.JoinedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.SkillLevelId).HasColumnName("SkillLevelID");
            entity.Property(e => e.Status).HasDefaultValue((byte)1);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Session).WithMany(p => p.SessionParticipants)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SessionParticipants_GameSessions");

            entity.HasOne(d => d.SkillLevel).WithMany(p => p.SessionParticipants)
                .HasForeignKey(d => d.SkillLevelId)
                .HasConstraintName("FK_SessionParticipants_OrganizerSkillLevels");

            entity.HasOne(d => d.User).WithMany(p => p.SessionParticipants)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SessionParticipants_Users");
        });

        modelBuilder.Entity<SessionWalkinGuest>(entity =>
        {
            entity.HasKey(e => e.WalkinId).HasName("PK__SessionW__1D594937536046A1");

            entity.Property(e => e.WalkinId).HasColumnName("WalkinID");
            entity.Property(e => e.AmountPaid).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.GuestName).HasMaxLength(150);
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.SkillLevelId).HasColumnName("SkillLevelID");
            entity.Property(e => e.Status).HasDefaultValue((byte)1);

            entity.HasOne(d => d.Session).WithMany(p => p.SessionWalkinGuests)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SessionWalkinGuests_GameSessions");

            entity.HasOne(d => d.SkillLevel).WithMany(p => p.SessionWalkinGuests)
                .HasForeignKey(d => d.SkillLevelId)
                .HasConstraintName("FK_SessionWalkinGuests_OrganizerSkillLevels");
        });

        modelBuilder.Entity<ShuttlecockBrand>(entity =>
        {
            entity.HasKey(e => e.BrandId).HasName("PK__Shuttlec__DAD4F3BEB35E75CA");

            entity.Property(e => e.BrandId).HasColumnName("BrandID");
            entity.Property(e => e.BrandName).HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<ShuttlecockModel>(entity =>
        {
            entity.HasKey(e => e.ModelId).HasName("PK__Shuttlec__E8D7A1CCDFD2363E");

            entity.Property(e => e.ModelId).HasColumnName("ModelID");
            entity.Property(e => e.BrandId).HasColumnName("BrandID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ModelName).HasMaxLength(100);

            entity.HasOne(d => d.Brand).WithMany(p => p.ShuttlecockModels)
                .HasForeignKey(d => d.BrandId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShuttlecockModels_Brands");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCAC380AE4AD");

            entity.HasIndex(e => e.UserPublicId, "UQ__Users__5676E5D09D724385").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UserPublicId).HasDefaultValueSql("(newid())");
        });

        modelBuilder.Entity<UserLogin>(entity =>
        {
            entity.HasKey(e => new { e.ProviderName, e.ProviderKey }).HasName("PK__UserLogi__85DB3F21998EA1A6");

            entity.Property(e => e.ProviderName).HasMaxLength(50);
            entity.Property(e => e.ProviderKey).HasMaxLength(255);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.ProviderEmail).HasMaxLength(255);
            entity.Property(e => e.RefreshToken).HasMaxLength(256);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.UserLogins)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserLogins_Users");
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__UserProf__1788CCACBEA7A31A");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("UserID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.EmergencyContactName).HasMaxLength(200);
            entity.Property(e => e.EmergencyContactPhone).HasMaxLength(20);
            entity.Property(e => e.FirstName).HasMaxLength(150);
            entity.Property(e => e.LastName).HasMaxLength(150);
            entity.Property(e => e.Nickname).HasMaxLength(100);
            entity.Property(e => e.Otpcode)
                .HasMaxLength(6)
                .HasColumnName("OTPCode");
            entity.Property(e => e.OtpexpiryDate).HasColumnName("OTPExpiryDate");
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.PrimaryContactEmail).HasMaxLength(255);
            entity.Property(e => e.ProfilePhotoUrl)
                .HasMaxLength(500)
                .HasColumnName("ProfilePhotoURL");

            entity.HasOne(d => d.User).WithOne(p => p.UserProfile)
                .HasForeignKey<UserProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserProfiles_Users");
        });

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.HasKey(e => e.VenueId).HasName("PK__Venues__3C57E5D2A616ED24");

            entity.HasIndex(e => e.GooglePlaceId, "UQ__Venues__A19E0A9088EE36C2").IsUnique();

            entity.Property(e => e.VenueId).HasColumnName("VenueID");
            entity.Property(e => e.FirstUsedByUserId).HasColumnName("FirstUsedByUserID");
            entity.Property(e => e.FirstUsedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.GooglePlaceId).HasMaxLength(255);
            entity.Property(e => e.Latitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.VenueName).HasMaxLength(255);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
