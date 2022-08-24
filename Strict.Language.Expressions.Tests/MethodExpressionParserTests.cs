using NUnit.Framework;
using Strict.Language.Tests;

namespace Strict.Language.Expressions.Tests;

public sealed class MethodExpressionParserTests : TestExpressions
{
	[Test]
	public void ParseSingleLine()
	{
		var body = new Method(type, 0, this, new[] { MethodTests.Run, MethodTests.LetNumber }).GetBodyAndParseIfNeeded();
		Assert.That(body.ReturnType, Is.EqualTo(type.FindType(Base.Number)));
		Assert.That(body, Is.TypeOf<Assignment>());
		Assert.That(body.ReturnType, Is.EqualTo(number.ReturnType));
		Assert.That(body.ToString(), Is.EqualTo(MethodTests.LetNumber[1..]));
	}

	[Test]
	public void ParseMultipleLines()
	{
		var body = (Body)new Method(type, 0, this, new[] { MethodTests.Run, MethodTests.LetNumber, MethodTests.LetOther }).GetBodyAndParseIfNeeded();
		Assert.That(body.Expressions, Has.Count.EqualTo(2));
		Assert.That(body.Expressions[0].ToString(), Is.EqualTo(MethodTests.LetNumber[1..]));
		Assert.That(body.Expressions[1].ToString(), Is.EqualTo(MethodTests.LetOther[1..]));
	}

	[Test]
	public void ParseNestedLines()
	{
		var body = (Body)new Method(type, 0, this, MethodTests.NestedMethodLines).GetBodyAndParseIfNeeded();
		Assert.That(body.Expressions, Has.Count.EqualTo(3));
		Assert.That(body.Expressions[0].ToString(), Is.EqualTo(MethodTests.LetNumber[1..]));
		Assert.That(body.Expressions[1].ToString(),
			Is.EqualTo(MethodTests.NestedMethodLines[2][1..] + "\r\n" + MethodTests.NestedMethodLines[3][1..]));
		Assert.That(body.Expressions[2].ToString(), Is.EqualTo("false"));
	}
}