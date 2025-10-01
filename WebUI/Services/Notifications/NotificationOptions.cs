namespace WebUI.Services.Notifications
{
    public class NotificationOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 25;
        public bool EnableSsl { get; set; } = true;
        public string UserName { get; set; } = ""; // SMTP kullanıcı adı
        public string Password { get; set; } = "";
        public string From { get; set; } = "";
        public string? FromName { get; set; }
    }
}
