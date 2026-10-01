using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Stenford.Domain.DataModels;

[Table("SHO_Showroom")]
public partial class ShoShowroom
{
    [Key]
    [Column("ShowroomID")]
    public int ShowroomId { get; set; }

    [StringLength(256)]
    public string ShowroomName { get; set; } = null!;

    public string? GoogleLink { get; set; }

    [StringLength(256)]
    public string DealerName { get; set; } = null!;

    [StringLength(256)]
    public string? ContactPersonName { get; set; }

    [StringLength(20)]
    public string PrimaryContact { get; set; } = null!;

    [StringLength(20)]
    public string? SecondaryContact { get; set; }

    public string Address { get; set; } = null!;

    [Column("CountryID")]
    public int CountryId { get; set; }

    [Column("StateID")]
    public int StateId { get; set; }

    [Column("CityID")]
    public int CityId { get; set; }

    public bool IsDeleted { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime ModifiedAt { get; set; }

    public Guid ModifiedBy { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    [Precision(10, 8)]
    public decimal? Latitude { get; set; }

    [Precision(11, 8)]
    public decimal? Longitude { get; set; }

    [ForeignKey("CityId")]
    [InverseProperty("ShoShowrooms")]
    public virtual LocCity City { get; set; } = null!;

    [ForeignKey("CountryId")]
    [InverseProperty("ShoShowrooms")]
    public virtual LocCountry Country { get; set; } = null!;

    [ForeignKey("StateId")]
    [InverseProperty("ShoShowrooms")]
    public virtual LocState State { get; set; } = null!;

    [InverseProperty("Showroom")]
    public virtual ICollection<VisVisit> VisVisits { get; set; } = new List<VisVisit>();
}
