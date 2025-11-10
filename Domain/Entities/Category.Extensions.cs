//using System;

//namespace Domain.Entities
//{
//    public partial class Category : IEntity
//    {
//        public void MarkAsDeleted(int deletedBy)
//        {
//            IsDeleted = true;
//            IsActive = false;
//            DeletedDate = DateTime.UtcNow;
//            DeletedBy = deletedBy;
//            UpdatedDate = DateTime.UtcNow;
//        }

//        public void Restore()
//        {
//            IsDeleted = false;
//            IsActive = true;
//            DeletedDate = null;
//            DeletedBy = null;
//            UpdatedDate = DateTime.UtcNow;
//        }
//    }
//}