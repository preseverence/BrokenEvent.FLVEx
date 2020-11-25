using System;

using NUnit.Framework;

namespace BrokenEvent.FLVEx.Tests
{
  [TestFixture]
  class ParserTests
  {
    [Test]
    public void Integer()
    {
      Assert.AreEqual(1, CmdModel.ParseSeconds("1"));
      Assert.AreEqual(0, CmdModel.ParseSeconds("0"));
      Assert.AreEqual(1000, CmdModel.ParseSeconds("1000"));
    }

    [Test]
    public void Invalid()
    {
      Assert.Throws<FormatException>(() => CmdModel.ParseSeconds("a"));
      Assert.Throws<FormatException>(() => CmdModel.ParseSeconds("b"));
      Assert.Throws<FormatException>(() => CmdModel.ParseSeconds("0:0:0:0"));
    }

    [Test]
    public void InvalidInteger()
    {
      Assert.Throws<FormatException>(() => CmdModel.ParseSeconds("a:b:c:d"));
    }

    [Test]
    public void TimeFormat()
    {
      Assert.AreEqual(1, CmdModel.ParseSeconds("0:1"));
      Assert.AreEqual(1, CmdModel.ParseSeconds("00:01"));
      Assert.AreEqual(1, CmdModel.ParseSeconds("00:00:01"));

      Assert.AreEqual(5141, CmdModel.ParseSeconds("01:25:41"));
      Assert.AreEqual(1541, CmdModel.ParseSeconds("25:41"));
    }
  }
}
