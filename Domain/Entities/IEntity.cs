using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public interface IEntity
    {
        int Id { get; set; }
        Guid PublicId { get; set; }
        DateTime? CreatedDate { get; set; }
        DateTime? UpdatedDate { get; set; }
        bool? IsActive { get; set; }
        bool IsDeleted { get; set; }
        DateTime? DeletedDate { get; set; }
        int? DeletedBy { get; set; }
        
    }
}
