namespace InstAnalytics.Models;

public class InstagramUser
{
    public string Username { get; set; } = string.Empty;
    public DateTime? FollowDate { get; set; }

    public InstagramUser(string username, DateTime? followDate = null)
    {
        Username = username;
        FollowDate = followDate;
    }
}
