using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Book : IEntity  //  : IEntity EKLEDİM
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string Author { get; set; } = null!;

    public string? Isbn { get; set; }

    public string? Publisher { get; set; }

    public int? PublishYear { get; set; }

    public int? CategoryId { get; set; }

    public int? TotalCopies { get; set; }

    public int? AvailableCopies { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public bool? IsActive { get; set; }

    public Guid PublicId { get; set; }

    public virtual Category? Category { get; set; }

    public virtual ICollection<Loan> Loans { get; set; } = new List<Loan>();
    // ✅ SOFT DELETE 
    public bool IsDeleted { get; set; }
    public DateTime? DeletedDate { get; set; }
    public int? DeletedBy { get; set; }
    
}
