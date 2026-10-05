using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Stenford.Domain.DataModels;

namespace Stenford.Domain.DataContext;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AspAspNetUser> AspAspNetUsers { get; set; }

    public virtual DbSet<AspAspNetUserRole> AspAspNetUserRoles { get; set; }

    public virtual DbSet<AspAspNetUserWiseRole> AspAspNetUserWiseRoles { get; set; }

    public virtual DbSet<LocCity> LocCities { get; set; }

    public virtual DbSet<LocCountry> LocCountries { get; set; }

    public virtual DbSet<LocState> LocStates { get; set; }

    public virtual DbSet<SecAdmin> SecAdmins { get; set; }

    public virtual DbSet<SecSalesPerson> SecSalesPeople { get; set; }

    public virtual DbSet<ShoShowroom> ShoShowrooms { get; set; }

    public virtual DbSet<VisVisit> VisVisits { get; set; }

    public virtual DbSet<VisVisitWiseAttachment> VisVisitWiseAttachments { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        //=> optionsBuilder.UseNpgsql("User ID=postgres;Password=0507;Server=localhost;Port=5432;Database=Stenford_DB;Pooling=true;");
        => optionsBuilder.UseNpgsql("User ID=stedfordceramic_user;Password=StendfordCeramic@111!!!;Server=168.231.120.235;Port=5432;Database=StenfordCeramicsAPP_DB;Pooling=true;");


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AspAspNetUser>(entity =>
        {
            entity.HasKey(e => e.AspNetUserId).HasName("ASP_AspNetUser_pkey");

            entity.Property(e => e.AspNetUserId).ValueGeneratedNever();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        });

        modelBuilder.Entity<AspAspNetUserRole>(entity =>
        {
            entity.HasKey(e => e.AspNetUserRoleId).HasName("ASP_AspNetUserRole_pkey");

            entity.Property(e => e.AspNetUserRoleId).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        });

        modelBuilder.Entity<AspAspNetUserWiseRole>(entity =>
        {
            entity.HasKey(e => e.AspNetUserWiseRoleId).HasName("ASP_AspNetUserWiseRole_pkey");

            entity.Property(e => e.AspNetUserWiseRoleId).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.AspNetUser).WithMany(p => p.AspAspNetUserWiseRoles)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ASP_AspNetUserWiseRole_ASP_AspNetUser");

            entity.HasOne(d => d.AspNetUserRole).WithMany(p => p.AspAspNetUserWiseRoles)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ASP_AspNetUserWiseRole_ASP_AspNetUserRole");
        });

        modelBuilder.Entity<LocCity>(entity =>
        {
            entity.HasKey(e => e.CityId).HasName("LOC_City_pkey");

            entity.Property(e => e.CityId).ValueGeneratedNever();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.Country).WithMany(p => p.LocCities)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LOC_City_LOC_Country");

            entity.HasOne(d => d.State).WithMany(p => p.LocCities)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LOC_City_LOC_State");
        });

        modelBuilder.Entity<LocCountry>(entity =>
        {
            entity.HasKey(e => e.CountryId).HasName("LOC_Country_pkey");

            entity.Property(e => e.CountryId).ValueGeneratedNever();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        });

        modelBuilder.Entity<LocState>(entity =>
        {
            entity.HasKey(e => e.StateId).HasName("LOC_State_pkey");

            entity.Property(e => e.StateId).ValueGeneratedNever();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.Country).WithMany(p => p.LocStates)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LOC_State_LOC_Country");
        });

        modelBuilder.Entity<SecAdmin>(entity =>
        {
            entity.HasKey(e => e.AdminId).HasName("SEC_Admin_pkey");

            entity.Property(e => e.AdminId).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.AspNetUser).WithMany(p => p.SecAdmins)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SEC_Admin_ASP_AspNetUser");

            entity.HasOne(d => d.Role).WithMany(p => p.SecAdmins)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SEC_Admin_ASP_AspNetUserRole");
        });

        modelBuilder.Entity<SecSalesPerson>(entity =>
        {
            entity.HasKey(e => e.SalesPersonId).HasName("SEC_SalesPerson_pkey");

            entity.Property(e => e.SalesPersonId).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.AspNetUser).WithMany(p => p.SecSalesPeople)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SEC_SalesPerson_ASP_AspNetUser");

            entity.HasOne(d => d.City).WithMany(p => p.SecSalesPeople)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SEC_SalesPerson_LOC_City");

            entity.HasOne(d => d.Country).WithMany(p => p.SecSalesPeople)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SEC_SalesPerson_LOC_Country");

            entity.HasOne(d => d.State).WithMany(p => p.SecSalesPeople)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SEC_SalesPerson_LOC_State");
        });

        modelBuilder.Entity<ShoShowroom>(entity =>
        {
            entity.HasKey(e => e.ShowroomId).HasName("SHO_Showroom_pkey");

            entity.Property(e => e.ShowroomId).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.City).WithMany(p => p.ShoShowrooms)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SHO_Showroom_LOC_City");

            entity.HasOne(d => d.Country).WithMany(p => p.ShoShowrooms)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SHO_Showroom_LOC_Country");

            entity.HasOne(d => d.State).WithMany(p => p.ShoShowrooms)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SHO_Showroom_LOC_State");
        });

        modelBuilder.Entity<VisVisit>(entity =>
        {
            entity.HasKey(e => e.VisitId).HasName("VIS_Visit_pkey");

            entity.Property(e => e.VisitId).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.SalesPerson).WithMany(p => p.VisVisits)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VIS_Visit_SEC_SalesPerson");

            entity.HasOne(d => d.Showroom).WithMany(p => p.VisVisits)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VIS_Visit_SHO_Showroom");
        });

        modelBuilder.Entity<VisVisitWiseAttachment>(entity =>
        {
            entity.HasKey(e => e.VisitAttachmentId).HasName("VIS_VisitWiseAttachment_pkey");

            entity.Property(e => e.VisitAttachmentId).UseIdentityAlwaysColumn();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.Visit).WithMany(p => p.VisVisitWiseAttachments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VIS_VisitWiseAttachment_VIS_Visit");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
