namespace ChatSystemNika.Model;

public class ChatMessage
{
    public string Name { get; set; }
    public string Message { get; set; }

    public override string ToString()
    {
        return $"{Name}:\n{Message}";
    }
}