namespace WebUI.Models
{
    public class Menu
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Url { get; set; }
        public int Order { get; set; }

        // Yeni:
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime Date
        {
            get => CreatedAt;
            set => CreatedAt = value;
        }
    }
}
