using System;
using Microsoft.EntityFrameworkCore;

namespace FetchDataFunctions.Models
{
    public partial class AlpinehutsDbContext : DbContext
    {
        public AlpinehutsDbContext(DbContextOptions<AlpinehutsDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Availability> Availability { get; set; }
        public virtual DbSet<Hut> Huts { get; set; }
        public virtual DbSet<BedCategory> BedCategories { get; set; }
        public virtual DbSet<FreeBedUpdateSubscription> FreeBedUpdateSubscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("ProductVersion", "2.2.4-servicing-10062");

            modelBuilder.Entity<Availability>(entity =>
            {
                entity.Property(e => e.Hutid).HasColumnName("hutid");

                entity.HasOne(d => d.Hut)
                    .WithMany(p => p.Availability)
                    .HasForeignKey(d => d.Hutid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Huts_Availability");

                entity.HasOne(a => a.BedCategory)
                    .WithMany(b => b.Availabilities)
                    .HasForeignKey(a => a.BedCategoryId);
            });

            modelBuilder.Entity<BedCategory>(entity =>
            {
                entity.HasOne(a => a.SharesNameWith)
                    .WithMany(b => b.SameNamesAsThis)
                    .HasForeignKey(c => c.SharesNameWithBedCateogryId)
                    .HasConstraintName("FK_BedCategory_SameAs");
            });

            modelBuilder.Entity<Hut>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.Added);
                entity.Property(e => e.Enabled);
                entity.Property(e => e.Activated);
                entity.Property(e => e.LastUpdated);

                entity.Property(e => e.Longitude);
                entity.Property(e => e.Latitude);
                entity.Property(e => e.Altitude);
                
                entity.Property(e => e.Country).HasMaxLength(100);

                entity.Property(e => e.Region).HasMaxLength(100);

                entity.Property(e => e.HutWebsite).HasMaxLength(100);

                entity.Property(e => e.Link).HasMaxLength(100);

                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<FreeBedUpdateSubscription>(entity => { entity.HasKey(pr => new { pr.HutId, pr.Date, pr.EmailAddress }); });
        }
    }
}