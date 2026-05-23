namespace SmartOffice.Api.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public long TelegramId { get; set; } 
        public string Name { get; set; }
        public string Email { get; set; }

        public User() { }

        public User(Guid id, long telegramId, string name, string email)
        {
            Id = id;
            TelegramId = telegramId;
            Name = name;
            Email = email;
        }
    }
}