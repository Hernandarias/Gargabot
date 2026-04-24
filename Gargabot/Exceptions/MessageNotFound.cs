namespace Gargabot.Exceptions
{
    public class MessageNotFound : Exception
    {
        public MessageNotFound(string message)
            : base("[Message Not Found] "+ message)
        {

        }
    }
}
