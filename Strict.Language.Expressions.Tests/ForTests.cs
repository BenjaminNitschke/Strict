﻿using NUnit.Framework;

namespace Strict.Language.Expressions.Tests;

public sealed class ForTests : TestExpressions
{
	[Test]
	public void MissingBody() =>
		Assert.That(() => ParseExpression("for Range(2, 5)"),
			Throws.InstanceOf<For.MissingInnerBody>());

	[Test]
	public void MissingExpression() =>
		Assert.That(() => ParseExpression("for"), Throws.InstanceOf<For.MissingExpression>());

	[Test]
	public void InvalidForExpression() =>
		Assert.That(() => ParseExpression("for gibberish", "\tlog.Write(\"Hi\")"),
			Throws.InstanceOf<IdentifierNotFound>());

	[Test]
	public void VariableOutOfScope() =>
		Assert.That(
			() => ParseExpression("for Range(2, 5)", "\tlet num = 5", "for Range(0, 10)",
				"\tlog.Write(num)"),
			Throws.InstanceOf<IdentifierNotFound>().With.Message.StartWith("num"));

	[Test]
	public void ValidExpressionType() =>
		Assert.That(() => ParseExpression("for Range(2, 5)", "\tlog.Write(\"Hi\")"),
			Is.TypeOf(typeof(For)));

	[Test]
	public void MatchingHashCode()
	{
		var forExpression = (For)ParseExpression("for Range(2, 5)", "\tlog.Write(index)");
		Assert.That(forExpression.GetHashCode(), Is.EqualTo(forExpression.Value.GetHashCode()));
	}

	[Test]
	public void IndexIsReserved() =>
		Assert.That(() => ParseExpression("for index in Range(0, 5)", "\tlog.Write(index)"),
			Throws.InstanceOf<For.IndexIsReserved>());

	[Test]
	public void UnidentifiedIterable() =>
		Assert.That(() => ParseExpression("for element in gibberish", "\tlog.Write(element)"),
			Throws.InstanceOf<For.UnidentifiedIterable>());

	[Test]
	public void DuplicateImplicitIndexInNestedFor() =>
		Assert.That(
			() => ParseExpression("for Range(2, 5)", "\tfor Range(0, 10)", "\t\tlog.Write(index)"),
			Throws.InstanceOf<For.DuplicateImplicitIndex>());

	[Test]
	public void ParseForRangeExpression() =>
		Assert.That(((For)ParseExpression("for Range(2, 5)", "\tlog.Write(index)")).ToString(),
			Is.EqualTo("for Range(2, 5)\n\tlog.Write(index)"));

	[Test]
	public void ParseForInExpression() =>
		Assert.That(
			((For)((Body)ParseExpression("let myIndex = 0", "for myIndex in Range(0, 5)",
				"\tlog.Write(myIndex)")).Expressions[1]).ToString(),
			Is.EqualTo("for myIndex in Range(0, 5)\n\tlog.Write(myIndex)"));

	[Test]
	public void ParseForInExpressionWithCustomVariableName() =>
		Assert.That(
			((For)ParseExpression("for myIndex in Range(0, 5)", "\tlog.Write(myIndex)")).ToString(),
			Is.EqualTo("for myIndex in Range(0, 5)\n\tlog.Write(myIndex)"));

	[Test]
	public void ParseForListExpression() =>
		Assert.That(((For)ParseExpression("for (1, 2, 3)", "\tlog.Write(index)")).ToString(),
			Is.EqualTo("for (1, 2, 3)\n\tlog.Write(index)"));

	[Test]
	public void ParseForListExpressionWithIterableVariable() =>
		Assert.That(
			((For)((Body)ParseExpression("let numbers = (1, 2, 3)", "for numbers",
				"\tlog.Write(index)")).Expressions[1]).ToString(),
			Is.EqualTo("for numbers\n\tlog.Write(index)"));

	[Test]
	public void ParseForListWithExplicitVariable() =>
		Assert.That(
			((For)((Body)ParseExpression( "let number = 0",
				"for number in (1, 2, 3)", "\tlog.Write(number)")).Expressions[1]).ToString(),
			Is.EqualTo("for number in (1, 2, 3)\n\tlog.Write(number)"));

	[Test]
	public void ParseWithNumber() =>
		Assert.That(
			((For)((Body)ParseExpression("let iterationCount = 10", "for iterationCount",
				"\tlog.Write(index)")).Expressions[1]).ToString(),
			Is.EqualTo("for iterationCount\n\tlog.Write(index)"));

	[Test]
	public void ParseNestedFor() =>
		//@formatter.off
		Assert.That(
			((For)ParseExpression("for myIndex in Range(2, 5)",
				"\tlog.Write(myIndex)",
				"\tfor Range(0, 10)",
				"\t\tlog.Write(index)")).ToString(),
			Is.EqualTo(
				"for myIndex in Range(2, 5)\n\tlog.Write(myIndex)\r\nfor Range(0, 10)\n\tlog.Write(index)"));

	[Test]
	public void ValidLoopProgram()
	{
		var programType = new Type(type.Package,
				new TypeLines(Base.App, "has n Number", "CountNumber Number", "\tlet result = Count(1)",
					"\tfor Range(0, n)", "\t\tresult.Increment", "\tresult")).
			ParseMembersAndMethods(new MethodExpressionParser());
		var parsedExpression = (Body)programType.Methods[0].GetBodyAndParseIfNeeded();
		var forMethodCall = (MethodCall)((For)parsedExpression.Expressions[1]).Body;
		Assert.That(parsedExpression.ReturnType.Name, Is.EqualTo(Base.Number));
		Assert.That(parsedExpression.Expressions[1], Is.TypeOf(typeof(For)));
		Assert.That(((For)parsedExpression.Expressions[1]).Value.ToString(),
			Is.EqualTo("Range(0, n)"));
		Assert.That(((VariableCall)forMethodCall.Instance!).Name, Is.EqualTo("result"));
		Assert.That(forMethodCall.Method.Name, Is.EqualTo("Increment"));
	}
}