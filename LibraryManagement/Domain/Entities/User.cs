//using Domain.Entities;
//using System;
//using System.Collections.Generic;

//namespace Domain.Entities;

//public partial class User : IEntity
//{
//    public int Id { get; set; }

//    public string Email { get; set; } = null!;

//    public string PasswordHash { get; set; } = null!;

//    public string FullName { get; set; } = null!;

//    public string Role { get; set; } = null!;

//    public DateTime? CreatedDate { get; set; }

//    public DateTime? UpdatedDate { get; set; }

//    public bool? IsActive { get; set; }

//    public bool TwoFactorEnabled { get; set; }

//    public string? TwoFactorSecretKey { get; set; }

//    public string? RefreshToken { get; set; }

//    public DateTime? RefreshTokenExpiryTime { get; set; }

//    public Guid PublicId { get; set; }

//    public bool IsDeleted { get; set; }

//    public DateTime? DeletedDate { get; set; }

//    public int? DeletedBy { get; set; }

//    public byte[] RowVersion { get; set; } = null!;

//    public virtual ICollection<Loan> Loans { get; set; } = new List<Loan>();
//}
