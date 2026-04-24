namespace Gargabot.Exceptions
{
    public class MessagesFileNotFound : Exception
    {
        public MessagesFileNotFound()
            : base($"Messages file not found")
        {

        }
    }
}
