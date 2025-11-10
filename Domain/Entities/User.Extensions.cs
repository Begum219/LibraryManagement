namespace Domain.Entities
{
    public partial class User : IEntity
    {
        // Soft Delete propertyler (Scaffold'da yok, buraya ekliyoruz)
        // IsDeleted, DeletedDate, DeletedBy zaten Scaffold tarafından eklendi
        
        // Helper metodlar
        public void MarkAsDeleted(int deletedBy)
        {
            IsDeleted = true;
            IsActive = false;
            DeletedDate = DateTime.UtcNow;
            DeletedBy = deletedBy;
            UpdatedDate = DateTime.UtcNow;
        }
        
        public void Restore()
        {
            IsDeleted = false;
            IsActive = true;
            DeletedDate = null;
            DeletedBy = null;
            UpdatedDate = DateTime.UtcNow;
        }
    }
}