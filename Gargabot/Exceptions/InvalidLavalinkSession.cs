namespace Gargabot.Exceptions
{
    public class InvalidLavalinkSession : Exception
    {
        public InvalidLavalinkSession(string message)
                : base("[InvalidLavalinkSession Exception] "+message)
        {
    
            }
    }
}
