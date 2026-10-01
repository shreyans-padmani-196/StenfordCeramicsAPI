using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Stenford.Domain.DataModels;

[Table("VIS_Visit")]
public partial class VisVisit
{
    [Key]
    [Column("VisitID")]
    public int VisitId { get; set; }

    [Column("ShowroomID")]
    public int ShowroomId { get; set; }

    [Column("SalesPersonID")]
    public int SalesPersonId { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime VisitDate { get; set; }

    [Precision(10, 8)]
    public decimal Latitude { get; set; }

    [Precision(11, 8)]
    public decimal Longitude { get; set; }

    public string DiscussionNotes { get; set; } = null!;

    public string? ProductsDiscussedString { get; set; }

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

    public string? VoiceNotePath { get; set; }

    public string? VisitingCardFrontPath { get; set; }

    public string? VisitingCardBackPath { get; set; }

    [ForeignKey("SalesPersonId")]
    [InverseProperty("VisVisits")]
    public virtual SecSalesPerson SalesPerson { get; set; } = null!;

    [ForeignKey("ShowroomId")]
    [InverseProperty("VisVisits")]
    public virtual ShoShowroom Showroom { get; set; } = null!;

    [InverseProperty("Visit")]
    public virtual ICollection<VisVisitWiseAttachment> VisVisitWiseAttachments { get; set; } = new List<VisVisitWiseAttachment>();
}
