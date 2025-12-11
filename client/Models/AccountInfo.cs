namespace client.Models
{
    public class AccountInfo
    {
        public string Username { get; set; } = "";
        public bool IsActive { get; set; }
        public string AvatarPath { get; set; } = "";  // можна додати потім
    }
}
