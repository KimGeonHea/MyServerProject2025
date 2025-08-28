using Google.Protobuf.Protocol;
using Google.Protobuf.WellKnownTypes;
using Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.Game.Object
{
  public class ObjectManager : Singleton<ObjectManager>
  {
    object _lock = new object();
    Dictionary<long, Hero> _heroes = new Dictionary<long, Hero>();

    // [OBJ_TYPE(4)][TEMPLATE_ID(20)][ID(20)]

    int _counter = 0;

    //public T Spawn<T>(int templateId = 0 ,int counter = 0) where T : BaseObject, new()
    //{
    //  T obj = new T();
    //
    //  //lock (_lock)
    //  //{
    //  //  obj.ObjectID = GenerateId(obj.ObjectType, templateId ,counter);
    //  //  
    //  //  if (obj.ObjectType == EGameObjectType.Hero)
    //  //    _heroes.Add(obj.ObjectID, obj as Hero);
    //  //}
    //  //
    //  //return obj;
    //
    //}

    // [OBJ_TYPE(4)][TEMPLATE_ID(20)][ID(20)]
    long GenerateId(EGameObjectType type, int templateId , int counter)
    {
      lock (_lock)
      {
        return ((long)((uint)type) << 40) | ((long)((uint)templateId) << 20) | (uint)(counter);
      }
    }

    // [OBJ_TYPE(4)][TEMPLATE_ID(20)][ID(20)]
    public static EGameObjectType GetObjectTypeFromId(long id)
    {
      int type = (int)((id >> 40) & 0x0F);
      return (EGameObjectType)type;
    }

    // [OBJ_TYPE(4)][TEMPLATE_ID(20)][ID(20)]
    public static int GetTemplateIdFromId(long id)
    {
      long templateId = ((id >> 20) & 0xFFFFF);
      return (int)templateId;
    }

    public bool Remove(long objectId)
    {
      EGameObjectType objectType = GetObjectTypeFromId(objectId);

      lock (_lock)
      {
        if (objectType == EGameObjectType.Hero)
          return _heroes.Remove(objectId);
      }

      return false;
    }

    public Hero FindHero(long objectId)
    {
      EGameObjectType objectType = GetObjectTypeFromId(objectId);

      lock (_lock)
      {
        if (objectType == EGameObjectType.Hero)
        {
          if (_heroes.TryGetValue(objectId, out Hero hero))
            return hero;
        }
      }

      return null;
    }
  }
}
