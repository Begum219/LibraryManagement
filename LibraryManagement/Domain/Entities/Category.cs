//using Domain.Entities;
//using System;
//using System.Collections.Generic;

//namespace Domain.Entities;

//public partial class Category : IEntity
//{
//    public int Id { get; set; }

//    public string Name { get; set; } = null!;

//    public string? Description { get; set; }

//    public DateTime? CreatedDate { get; set; }

//    public DateTime? UpdatedDate { get; set; }

//    public bool? IsActive { get; set; }

//    public Guid PublicId { get; set; }

//    public bool IsDeleted { get; set; }

//    public DateTime? DeletedDate { get; set; }

//    public int? DeletedBy { get; set; }

//    public byte[] RowVersion { get; set; } = null!;

//    public virtual ICollection<Book> Books { get; set; } = new List<Book>();
//}
