using System.ComponentModel.DataAnnotations;

namespace Marketplace.API.Domain
{
    public class UpdateStatusRequest
    {
        [Required]
        public string Status { get; set; }
    }
}
