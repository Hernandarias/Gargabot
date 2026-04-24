namespace Gargabot.Exceptions
{
    public class InvalidMessagesFormat : Exception
    {
        public InvalidMessagesFormat()
            : base("Messages file has wrong format")
        {

        }
    }
}
