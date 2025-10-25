using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Loan
{
    public int Id { get; set; }

    public int? BookId { get; set; }

    public int? UserId { get; set; }

    public DateTime? LoanDate { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    public bool? IsReturned { get; set; }

    public decimal? Fine { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public bool? IsActive { get; set; }

    public virtual Book? Book { get; set; }

    public virtual User? User { get; set; }
}
