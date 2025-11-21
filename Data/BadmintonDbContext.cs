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
            entity.HasKey(e => e.BankId).HasName("Banks_pkey");

            entity.Property(e => e.BankId).HasColumnName("BankID");
            entity.Property(e => e.BankCode).HasMaxLength(10);
            entity.Property(e => e.BankName).HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<BillLineItem>(entity =>
        {
            entity.HasKey(e => e.LineItemId).HasName("BillLineItems_pkey");

            entity.Property(e => e.LineItemId).HasColumnName("LineItemID");
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.BillId).HasColumnName("BillID");
            entity.Property(e => e.Description).HasMaxLength(255);

            entity.HasOne(d => d.Bill).WithMany(p => p.BillLineItems)
                .HasForeignKey(d => d.BillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BillLineItems_BillID");
        });

        modelBuilder.Entity<Facility>(entity =>
        {
            entity.HasKey(e => e.FacilityId).HasName("Facilities_pkey");

            entity.Property(e => e.FacilityId).HasColumnName("FacilityID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.FacilityName).HasMaxLength(100);
            entity.Property(e => e.IconUrl).HasMaxLength(250);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("GameSessions_pkey");

            entity.HasIndex(e => e.SessionPublicId, "GameSessions_SessionPublicId_key").IsUnique();

            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.CourtFeePerPerson).HasPrecision(10, 2);
            entity.Property(e => e.CourtNumbers).HasMaxLength(100);
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.GameTypeId).HasColumnName("GameTypeID");
            entity.Property(e => e.GroupName).HasMaxLength(255);
            entity.Property(e => e.PairingMethodId).HasColumnName("PairingMethodID");
            entity.Property(e => e.SessionPublicId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ShuttlecockCostPerUnit).HasPrecision(10, 2);
            entity.Property(e => e.ShuttlecockFeePerPerson).HasPrecision(10, 2);
            entity.Property(e => e.ShuttlecockModelId).HasColumnName("ShuttlecockModelID");
            entity.Property(e => e.Status).HasDefaultValue((short)1);
            entity.Property(e => e.TotalCourtCost).HasPrecision(10, 2);
            entity.Property(e => e.VenueId).HasColumnName("VenueID");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.GameSessions)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameSessions_CreatedByUserID");

            entity.HasOne(d => d.GameType).WithMany(p => p.GameSessions)
                .HasForeignKey(d => d.GameTypeId)
                .HasConstraintName("FK_GameSessions_GameTypeID");

            entity.HasOne(d => d.PairingMethod).WithMany(p => p.GameSessions)
                .HasForeignKey(d => d.PairingMethodId)
                .HasConstraintName("FK_GameSessions_PairingMethodID");

            entity.HasOne(d => d.ShuttlecockModel).WithMany(p => p.GameSessions)
                .HasForeignKey(d => d.ShuttlecockModelId)
                .HasConstraintName("FK_GameSessions_ShuttlecockModelID");

            entity.HasOne(d => d.Venue).WithMany(p => p.GameSessions)
                .HasForeignKey(d => d.VenueId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameSessions_VenueID");
        });

        modelBuilder.Entity<GameSessionFacility>(entity =>
        {
            entity.HasKey(e => new { e.SessionId, e.FacilityId }).HasName("GameSessionFacilities_pkey");

            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.FacilityId).HasColumnName("FacilityID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Facility).WithMany(p => p.GameSessionFacilities)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameSessionFacilities_FacilityID");

            entity.HasOne(d => d.Session).WithMany(p => p.GameSessionFacilities)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameSessionFacilities_SessionID");
        });

        modelBuilder.Entity<GameSessionPhoto>(entity =>
        {
            entity.HasKey(e => e.PhotoId).HasName("GameSessionPhotos_pkey");

            entity.Property(e => e.PhotoId).HasColumnName("PhotoID");
            entity.Property(e => e.Caption).HasMaxLength(255);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.PhotoUrl)
                .HasMaxLength(500)
                .HasColumnName("PhotoURL");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");

            entity.HasOne(d => d.Session).WithMany(p => p.GameSessionPhotos)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameSessionPhotos_SessionID");
        });

        modelBuilder.Entity<GameType>(entity =>
        {
            entity.HasKey(e => e.GameTypeId).HasName("GameTypes_pkey");

            entity.Property(e => e.GameTypeId).HasColumnName("GameTypeID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TypeName).HasMaxLength(100);
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(e => e.MatchId).HasName("Matches_pkey");

            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");

            entity.HasOne(d => d.Session).WithMany(p => p.Matches)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Matches_SessionID");
        });

        modelBuilder.Entity<MatchPlayer>(entity =>
        {
            entity.HasKey(e => e.MatchPlayerId).HasName("MatchPlayers_pkey");

            entity.Property(e => e.MatchPlayerId).HasColumnName("MatchPlayerID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.Team).HasMaxLength(1);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.WalkinId).HasColumnName("WalkinID");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchPlayers)
                .HasForeignKey(d => d.MatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchPlayers_MatchID");

            entity.HasOne(d => d.User).WithMany(p => p.MatchPlayers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_MatchPlayers_UserID");

            entity.HasOne(d => d.Walkin).WithMany(p => p.MatchPlayers)
                .HasForeignKey(d => d.WalkinId)
                .HasConstraintName("FK_MatchPlayers_WalkinID");
        });

        modelBuilder.Entity<OrganizerProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("OrganizerProfiles_pkey");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("UserID");
            entity.Property(e => e.BankAccountNumber).HasMaxLength(50);
            entity.Property(e => e.BankAccountPhotoUrl)
                .HasMaxLength(500)
                .HasColumnName("BankAccountPhotoURL");
            entity.Property(e => e.BankId).HasColumnName("BankID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.FacebookLink).HasMaxLength(500);
            entity.Property(e => e.FacebookVisibility).HasDefaultValue((short)0);
            entity.Property(e => e.LineId)
                .HasMaxLength(100)
                .HasColumnName("LineID");
            entity.Property(e => e.LineVisibility).HasDefaultValue((short)0);
            entity.Property(e => e.NationalId)
                .HasMaxLength(255)
                .HasColumnName("NationalID");
            entity.Property(e => e.PhoneVisibility).HasDefaultValue((short)0);
            entity.Property(e => e.ProfilePhotoUrl)
                .HasMaxLength(500)
                .HasColumnName("ProfilePhotoURL");
            entity.Property(e => e.PublicPhoneNumber).HasMaxLength(20);
            entity.Property(e => e.Status).HasDefaultValue((short)0);

            entity.HasOne(d => d.Bank).WithMany(p => p.OrganizerProfiles)
                .HasForeignKey(d => d.BankId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizerProfiles_BankID");

            entity.HasOne(d => d.User).WithOne(p => p.OrganizerProfile)
                .HasForeignKey<OrganizerProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizerProfiles_UserID");
        });

        modelBuilder.Entity<OrganizerSkillLevel>(entity =>
        {
            entity.HasKey(e => e.SkillLevelId).HasName("OrganizerSkillLevels_pkey");

            entity.Property(e => e.SkillLevelId).HasColumnName("SkillLevelID");
            entity.Property(e => e.ColorHexCode).HasMaxLength(7);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LevelName).HasMaxLength(50);
            entity.Property(e => e.OrganizerUserId).HasColumnName("OrganizerUserID");

            entity.HasOne(d => d.OrganizerUser).WithMany(p => p.OrganizerSkillLevels)
                .HasForeignKey(d => d.OrganizerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizerSkillLevels_OrganizerUserID");
        });

        modelBuilder.Entity<PairingMethod>(entity =>
        {
            entity.HasKey(e => e.PairingMethodId).HasName("PairingMethods_pkey");

            entity.Property(e => e.PairingMethodId).HasColumnName("PairingMethodID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MethodName).HasMaxLength(100);
        });

        modelBuilder.Entity<ParticipantBill>(entity =>
        {
            entity.HasKey(e => e.BillId).HasName("ParticipantBills_pkey");

            entity.Property(e => e.BillId).HasColumnName("BillID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.TotalAmount).HasPrecision(10, 2);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.WalkinId).HasColumnName("WalkinID");

            entity.HasOne(d => d.Session).WithMany(p => p.ParticipantBills)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ParticipantBills_SessionID");

            entity.HasOne(d => d.User).WithMany(p => p.ParticipantBills)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_ParticipantBills_UserID");

            entity.HasOne(d => d.Walkin).WithMany(p => p.ParticipantBills)
                .HasForeignKey(d => d.WalkinId)
                .HasConstraintName("FK_ParticipantBills_WalkinID");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("Payments_pkey");

            entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.BillId).HasColumnName("BillID");
            entity.Property(e => e.PaymentDate).HasDefaultValueSql("now()");
            entity.Property(e => e.ReceivedByUserId).HasColumnName("ReceivedByUserID");

            entity.HasOne(d => d.Bill).WithMany(p => p.Payments)
                .HasForeignKey(d => d.BillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_BillID");
        });

        modelBuilder.Entity<SessionParticipant>(entity =>
        {
            entity.HasKey(e => e.ParticipantId).HasName("SessionParticipants_pkey");

            entity.Property(e => e.ParticipantId).HasColumnName("ParticipantID");
            entity.Property(e => e.JoinedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.SkillLevelId).HasColumnName("SkillLevelID");
            entity.Property(e => e.Status).HasDefaultValue((short)1);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Session).WithMany(p => p.SessionParticipants)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SessionParticipants_SessionID");

            entity.HasOne(d => d.SkillLevel).WithMany(p => p.SessionParticipants)
                .HasForeignKey(d => d.SkillLevelId)
                .HasConstraintName("FK_SessionParticipants_SkillLevelID");

            entity.HasOne(d => d.User).WithMany(p => p.SessionParticipants)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SessionParticipants_UserID");
        });

        modelBuilder.Entity<SessionWalkinGuest>(entity =>
        {
            entity.HasKey(e => e.WalkinId).HasName("SessionWalkinGuests_pkey");

            entity.Property(e => e.WalkinId).HasColumnName("WalkinID");
            entity.Property(e => e.AmountPaid).HasPrecision(10, 2);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.GuestName).HasMaxLength(150);
            entity.Property(e => e.SessionId).HasColumnName("SessionID");
            entity.Property(e => e.SkillLevelId).HasColumnName("SkillLevelID");
            entity.Property(e => e.Status).HasDefaultValue((short)1);

            entity.HasOne(d => d.Session).WithMany(p => p.SessionWalkinGuests)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SessionWalkinGuests_SessionID");

            entity.HasOne(d => d.SkillLevel).WithMany(p => p.SessionWalkinGuests)
                .HasForeignKey(d => d.SkillLevelId)
                .HasConstraintName("FK_SessionWalkinGuests_SkillLevelID");
        });

        modelBuilder.Entity<ShuttlecockBrand>(entity =>
        {
            entity.HasKey(e => e.BrandId).HasName("ShuttlecockBrands_pkey");

            entity.Property(e => e.BrandId).HasColumnName("BrandID");
            entity.Property(e => e.BrandName).HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<ShuttlecockModel>(entity =>
        {
            entity.HasKey(e => e.ModelId).HasName("ShuttlecockModels_pkey");

            entity.Property(e => e.ModelId).HasColumnName("ModelID");
            entity.Property(e => e.BrandId).HasColumnName("BrandID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ModelName).HasMaxLength(100);

            entity.HasOne(d => d.Brand).WithMany(p => p.ShuttlecockModels)
                .HasForeignKey(d => d.BrandId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShuttlecockModels_BrandID");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("Users_pkey");

            entity.HasIndex(e => e.UserPublicId, "Users_UserPublicId_key").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UserPublicId).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<UserLogin>(entity =>
        {
            entity.HasKey(e => new { e.ProviderName, e.ProviderKey }).HasName("UserLogins_pkey");

            entity.Property(e => e.ProviderName).HasMaxLength(50);
            entity.Property(e => e.ProviderKey).HasMaxLength(255);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.ProviderEmail).HasMaxLength(255);
            entity.Property(e => e.RefreshToken).HasMaxLength(256);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.UserLogins)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserLogins_UserID");
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("UserProfiles_pkey");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("UserID");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.EmergencyContactName).HasMaxLength(200);
            entity.Property(e => e.EmergencyContactPhone).HasMaxLength(20);
            entity.Property(e => e.FirstName).HasMaxLength(150);
            entity.Property(e => e.IsPhoneNumberVerified).HasDefaultValue(false);
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
                .HasConstraintName("FK_UserProfiles_UserID");
        });

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.HasKey(e => e.VenueId).HasName("Venues_pkey");

            entity.HasIndex(e => e.GooglePlaceId, "Venues_GooglePlaceId_key").IsUnique();

            entity.Property(e => e.VenueId).HasColumnName("VenueID");
            entity.Property(e => e.FirstUsedByUserId).HasColumnName("FirstUsedByUserID");
            entity.Property(e => e.FirstUsedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.GooglePlaceId).HasMaxLength(255);
            entity.Property(e => e.Latitude).HasPrecision(9, 6);
            entity.Property(e => e.Longitude).HasPrecision(9, 6);
            entity.Property(e => e.VenueName).HasMaxLength(255);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
