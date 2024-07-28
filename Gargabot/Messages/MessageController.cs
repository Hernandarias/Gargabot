using Gargabot.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Messages
{
    public class MessageController
    {
        private Dictionary<string, string> messages;

        public MessageController(string filePath)
        {
            if (!File.Exists(filePath))
                throw new MessagesFileNotFound();

            var json = File.ReadAllText(filePath);
            try
            {
                messages = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            catch
            {
                throw new InvalidMessagesFormat();
            }

            foreach(string message in MessageList.messages)
            {
                if (!messages.ContainsKey(message))
                {
                    throw new MessageNotFound(message);
                }
            }
        }

        public string GetMessage(string key, params object[] args)
        {
            if (!messages.ContainsKey(key))
            {
                return "[Message Not Found] " + key;

            }

            return string.Format(messages[key], args);
        }
    }

}
