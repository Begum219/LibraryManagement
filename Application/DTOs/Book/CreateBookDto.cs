namespace LibraryManagement.Application.DTOs.Book
{
    public class CreateBookDto
    {
        public string Title { get; set; } = null!;
        public string Author { get; set; } = null!;
        public string? Isbn { get; set; }
        public string? Publisher { get; set; }
        public int? PublishYear { get; set; }
        public int CategoryId { get; set; }
        public int TotalCopies { get; set; }
    }
}