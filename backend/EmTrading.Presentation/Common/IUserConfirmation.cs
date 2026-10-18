namespace EmTrading.Presentation.Common;

/// <summary>Asks the user to confirm an action; implemented by the host with a dialog.</summary>
public interface IUserConfirmation
{
    bool Confirm(string title, string message);
}
