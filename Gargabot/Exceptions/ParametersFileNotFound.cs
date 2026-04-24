namespace Gargabot.Exceptions
{
    public class ParametersFileNotFound : Exception
    {
        public ParametersFileNotFound()
            : base($"Parameters file not found")
        {

        }
    }
}
