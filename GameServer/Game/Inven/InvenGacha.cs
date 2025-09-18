using GameServer;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Game
{

  // 가챠 드랍 정의(예시 데이터 구조)
  public class GachaDrop
  {
    public int TemplateId;
    public EItemType ItemType;
    public int CountMin = 1;
    public int CountMax = 1;
    public int Weight; // 가중치
  }

  public class InvenGacha
  {
    public InvenGacha(Player owner)
    {
      Owner = owner;
    }
    public Player Owner { get; set; }

    readonly Dictionary<int/*TemplteId */ , int /*pityCount*/> pityChaBox = new();

    public void Init(PlayerDb playerDb)
    {
      pityChaBox.Clear();
      if (playerDb?.Gachas == null) 
        return;


      foreach (var row in playerDb.Gachas)
        pityChaBox[row.TemplateId] = row.PityCount;
    }

    public int GetPity(int templateId)
      => pityChaBox.TryGetValue(templateId, out var v) ? v : 0;

    public void SetPity(int templateId, int value)
      => pityChaBox[templateId] = Math.Max(0, value);

    /// <summary>
    /// 메모리에 반영 + DB 업서트까지 한번에 처리
    /// </summary>
    public void ApplyAndPersistPity(int templateId, int pity)
    {
      SetPity(templateId, pity);
      DBManager.SavePityAsync(Owner, templateId, pity);
    }
  }
}
