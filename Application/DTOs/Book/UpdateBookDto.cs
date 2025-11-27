using System.Text.Json.Serialization;
using LibraryManagement.Application.Converters;

namespace LibraryManagement.Application.DTOs.Book
{
    public class UpdateBookDto
    {
        public string Title { get; set; } = null!;
        public string Author { get; set; } = null!;
        public string? Isbn { get; set; }
        public string? Publisher { get; set; }
        public int? PublishYear { get; set; }
        public int CategoryId { get; set; }
        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }

        [JsonConverter(typeof(Base64Converter))]
        public byte[]? RowVersion { get; set; }
    }
}