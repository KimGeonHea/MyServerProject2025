using Google.Protobuf.Protocol;
using Server;
using Server.Data;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Game.Room
{
  public partial class LobbyRoom : Room
  {
    const int PITY_THRESHOLD = 30; // 필요시 조정

    public void HandleUseItem(Player player, C_UseItem req)
    {
      if (player == null) return;

      // 사용할 상자 아이템
      Item boxItem = player.inventory.GetItemByDbId(req.ItemDbId);
      if (boxItem == null)
      { 
        SendToast(player, "아이템이 없습니다."); 
        return; 
      }

      // 1개/10개
      int boxCount = req.ConsumType switch
      {
        EConsumableType.RandomitemBox => 1,
        EConsumableType.RandomitemBoxTen => 10,
        _ => 0
      };
      if (boxCount <= 0) 
      {
        SendToast(player, "잘못된 소비 타입입니다."); 
        return; 
      }

      // 상자 타입 검증
      if (boxItem.ConsumableType != EConsumableType.RandomitemBox)
      {
        SendToast(player, "사용할 수 없는 아이템입니다.");
        return;
      }

      // 수량 체크
      if (boxItem.Count < boxCount)
      {
        SendToast(player, "상자 수량이 부족합니다.");
        return;
      }

      //인벤 용량 체크
      if(player.inventory.IsInventoryFull(boxCount))
      {
        SendToast(player, "인벤토리 용량이 부족합니다.");
        return;
      }


     OpenItemBoxWithPity(player, boxItem, boxCount);
    }

    private void OpenItemBoxWithPity(Player player, Item boxItem, int boxCount)
    {
      if (player == null || boxItem == null) return;

      int boxTemplateId = boxItem.TemplateId;

      // 현재 피티(메모리 캐시)
      int pity = player.invenGacha.GetPity(boxTemplateId);

      // 결과로 내려줄 아이템들
      var rewardInfos = new List<ItemInfo>();

      for (int i = 0; i < boxCount; i++)
      {
        bool forceAncient = ItemBox.HasAnyAncient() && pity >= (PITY_THRESHOLD - 1);

        // 드랍 계산
        ItemData reward;
        if (forceAncient)
        {
          reward = ItemBox.PickOneFromAncient();
          if (reward == null) { SendToast(player, "상자(Ancient) 풀이 비어있습니다."); return; }
          pity = 0; // 보장 발동 리셋
        }
        else
        {
          if (!ItemBox.OpenOnce(out reward) || reward == null)
          {
            SendToast(player, "상자 데이터 오류");
            return;
          }
          pity = ItemBox.IsAncient(reward) ? 0 : pity + 1;
        }
        
        // ===== 항상 신규 아이템 생성 =====
        var newItemDb = new ItemDb
        {

          ItemDbId = DBManager.GenerateItemDbId(), // 프로젝트의 제너레이터 사용
          PlayerDbId = player.PlayerDbId,
          TemplateId = reward.TemplateId,
          Count = 1,
          EnchantCount = 0,
          EquipSlot = EItemSlotType.Inventory,
        };

        // 메모리 반영 (S_AddItem 안 보냄)
        var newItem = Item.MakeItem(newItemDb);
        player.inventory.Add(newItem, sendToClient: false);

        // DB Insert (잡큐)
        DBManager.SaveItemDbChanges(player, newItemDb, stackItemDb: null);

        // 응답 리스트에 추가
        rewardInfos.Add(newItem.Info);
      }

      // 상자 스택 차감(일괄)
      player.inventory.AddCount(boxItem.ItemDbId, -boxCount, sendToClient: true);

      // 피티 메모리 + DB 업서트
      player.invenGacha.ApplyAndPersistPity(boxTemplateId, pity);


      // ===== 결과 패킷 전송 (보상 + 가챠 상태) =====
      var res = new S_UseItemBox
      {
        GachaInfo = new GachaInfo
        {
          TemplateId = boxTemplateId,
          PityCount = pity
        }
      };

      res.Rewards.AddRange(rewardInfos);

      player.Session?.Send(res);
    }
    private void SendToast(Player player, string msg) 
    {
      /* ... */ 
    }
  }
}

