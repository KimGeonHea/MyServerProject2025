using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game
{
  public class ChatManager
  {
    public Queue<ChatMessage> globalChatQueue = new Queue<ChatMessage>();
    public const int maxCount = 20;
    // 채팅 메시지 추가
    public ChatMessage AddorGetChatMessage(int playerId,string playerName, string message)
    {
      var chatMessage = new ChatMessage
      {
        PlayerDbId = playerId,
        PlayerName = playerName,
        TextMessage = message,
        ChatTime = DateTime.Now.ToString()
      };

      // 글로벌 채팅에 메시지 추가
      globalChatQueue.Enqueue(chatMessage);

      // 200개 초과 시 가장 오래된 메시지 삭제
      if (globalChatQueue.Count > maxCount)
      {
        globalChatQueue.Dequeue(); // 가장 오래된 메시지 삭제
      }
      return chatMessage;
    }

     
    public List<ChatMessage> GetAllChatMessages()
    {
      return globalChatQueue.ToList();
    }
  }
}
