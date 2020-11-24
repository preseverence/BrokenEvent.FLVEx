using System.Collections.Generic;

using BrokenEvent.LibFLVEx.Utils;

using NUnit.Framework;

namespace BrokenEvent.FLVEx.Tests
{
  [TestFixture]
  class RemoverTests
  {
    [Test]
    public void RemoveSimple1()
    {
      List<int> list = new List<int>()
      {
        0,0,1,2,0,1,0,0,1
      };

      Remover<int> remover = new Remover<int>(list);

      int i = 0;
      while (i < list.Count)
      {
        if (list[i] == 0)
          remover.Remove(ref i);
        else
          remover.Skip(ref i);
      }

      remover.Finish(list.Count);

      Assert.AreEqual(remover.RemovedCount, 5);
      CollectionAssert.AreEqual(new int[] { 1, 2, 1, 1 }, list);
    }

    [Test]
    public void RemoveSimple2()
    {
      List<int> list = new List<int>()
      {
        1,2,0,1,0,0,1,0,0
      };

      Remover<int> remover = new Remover<int>(list);

      int i = 0;
      while (i < list.Count)
      {
        if (list[i] == 0)
          remover.Remove(ref i);
        else
          remover.Skip(ref i);
      }

      remover.Finish(list.Count);

      Assert.AreEqual(remover.RemovedCount, 5);
      CollectionAssert.AreEqual(new int[] { 1, 2, 1, 1 }, list);
    }

    [Test]
    public void RemoveWithLimit1()
    {
      List<int> list = new List<int>()
      {
        1,2,0,1,0,0,1
      };

      Remover<int> remover = new Remover<int>(list, 5);

      int i = 0;
      while (i < remover.Limit)
      {
        if (list[i] == 0)
          remover.Remove(ref i);
        else
          remover.Skip(ref i);
      }

      remover.Finish(remover.Limit);

      Assert.AreEqual(remover.RemovedCount, 2);
      CollectionAssert.AreEqual(new int[] { 1, 2, 1, 0, 1 }, list);
    }

    [Test]
    public void RemoveWithLimit2()
    {
      List<int> list = new List<int>()
      {
        1,2,0,1,0,0,1,0,0
      };

      Remover<int> remover = new Remover<int>(list, 5);

      int i = 0;
      while (i < remover.Limit)
      {
        if (list[i] == 0)
          remover.Remove(ref i);
        else
          remover.Skip(ref i);
      }

      remover.Finish(remover.Limit);

      Assert.AreEqual(remover.RemovedCount, 2);
      CollectionAssert.AreEqual(new int[] { 1, 2, 1, 0, 1, 0, 0 }, list);
    }
  }
}
