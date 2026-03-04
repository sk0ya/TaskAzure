namespace TaskAzure.Models;

public class AdoUser
{
    public string DisplayName { get; set; } = "";
    /// <summary>メールアドレス (WorkItem の AssignedTo に使用する値)</summary>
    public string UniqueName { get; set; } = "";

    public override string ToString() => DisplayName;
}
