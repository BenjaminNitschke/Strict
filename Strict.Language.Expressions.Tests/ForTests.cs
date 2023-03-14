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
	public void VariableOutOfScope() =>
		Assert.That(
			() => ParseExpression("for Range(2, 5)", "\tconstant num = 5", "for Range(0, 10)",
				"\tlog.Write(num)"),
			Throws.InstanceOf<Body.IdentifierNotFound>().With.Message.StartWith("num"));

	[Test]
	public void Equals()
	{
		var first = ParseExpression("for Range(2, 5)", "\tlog.Write(\"Hi\")");
		var second = ParseExpression("for Range(2, 5)", "\tlog.Write(\"Hi\")");
		Assert.That(first, Is.InstanceOf<For>());
		Assert.That(first.Equals(second), Is.True);
	}

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

	[TestCase("for gibberish", "\tlog.Write(\"Hi\")")]
	[TestCase("for element in gibberish", "\tlog.Write(element)")]
	public void UnidentifiedIterable(params string[] lines) =>
		Assert.That(() => ParseExpression(lines),
			Throws.InstanceOf<Body.IdentifierNotFound>());

	[Test]
	public void DuplicateImplicitIndexInNestedFor() =>
		Assert.That(
			() => ParseExpression("for Range(2, 5)", "\tfor Range(0, 10)", "\t\tlog.Write(index)"),
			Throws.InstanceOf<For.DuplicateImplicitIndex>());

	[Test]
	public void ImmutableVariableNotAllowedToBeAnIterator() =>
		Assert.That(
			() => ParseExpression("constant myIndex = 0", "for myIndex in Range(0, 10)",
				"\tlog.Write(myIndex)"), Throws.InstanceOf<For.ImmutableIterator>());

	[Test]
	public void IteratorHasMatchingTypeWithIterable() =>
		Assert.That(() =>
				ParseExpression("mutable element = 0",
					"for element in (\"1\", \"2\", \"3\")", "\tlog.Write(element)"),
			Throws.InstanceOf<For.IteratorTypeDoesNotMatchWithIterable>());

	[Test]
	public void ParseForRangeExpression() =>
		Assert.That(((For)ParseExpression("for Range(2, 5)", "\tlog.Write(index)")).ToString(),
			Is.EqualTo("for Range(2, 5)\n\tlog.Write(index)"));

	[Test]
	public void ParseForInExpression() =>
		Assert.That(
			((For)((Body)ParseExpression("mutable myIndex = 0", "for myIndex in Range(0, 5)",
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
	public void ParseForListExpressionWithValue() =>
		Assert.That(((For)ParseExpression("for (1, 2, 3)", "\tlog.Write(value)")).ToString(),
			Is.EqualTo("for (1, 2, 3)\n\tlog.Write(value)"));

	[Test]
	public void ValidIteratorReturnTypeWithValue() =>
		Assert.That(
			((VariableCall)((MethodCall)((For)ParseExpression("for (1, 2, 3)", "\tlog.Write(value)")).
				Body).Arguments[0]).ReturnType.FullName, Is.EqualTo("TestPackage.Number"));

	[Test]
	public void ParseForListExpressionWithIterableVariable() =>
		Assert.That(
			((For)((Body)ParseExpression("constant elements = (1, 2, 3)", "for elements",
				"\tlog.Write(index)")).Expressions[1]).ToString(),
			Is.EqualTo("for elements\n\tlog.Write(index)"));

	[Test]
	public void ParseForListWithExplicitVariable() =>
		Assert.That(
			((For)((Body)ParseExpression("mutable element = 0", "for element in (1, 2, 3)",
				"\tlog.Write(element)")).Expressions[1]).ToString(),
			Is.EqualTo("for element in (1, 2, 3)\n\tlog.Write(element)"));

	[Test]
	public void ParseWithNumber() =>
		Assert.That(
			((For)((Body)ParseExpression("constant iterationCount = 10", "for iterationCount",
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

	[Ignore("TODO: Iterator is Generic now so should be replaced with proper types for GenericTypeImplementation types eg. Characters")]
	[Test]
	public void ParseForWithListOfTexts() =>
		Assert.That(
			() => ((For)((Body)ParseExpression("mutable element = \"1\"",
					"for element in (\"1\", \"2\", \"3\")", "\tlog.Write(element)")).Expressions[1]).
				ToString(), Is.EqualTo("for element in (\"1\", \"2\", \"3\")\n\tlog.Write(element)"));

	[Test]
	public void ValidIteratorReturnTypeForRange() =>
		Assert.That(
			((MethodCall)((For)ParseExpression("for Range(0, 10)", "\tlog.Write(index)")).Body).
			Arguments[0].ReturnType.Name == Base.Number);

	[Ignore("TODO: Iterator is Generic now so should be replaced with proper types for GenericTypeImplementation types eg. Characters")]
	[Test]
	public void ValidIteratorReturnTypeTextForList() =>
		Assert.That(
			((VariableCall)((MethodCall)((For)((Body)ParseExpression("mutable element = \"1\"",
					"for element in (\"1\", \"2\", \"3\")", "\tlog.Write(element)")).Expressions[1]).Body).
				Arguments[0]).CurrentValue.ReturnType.Name == Base.Text);

	[Test]
	public void ValidLoopProgram()
	{
		var programType = new Type(type.Package,
				new TypeLines(Base.App, "has number", "CountNumber Number", "\tmutable result = 1",
					"\tfor Range(0, number)", "\t\tresult = result + 1", "\tresult")).
			ParseMembersAndMethods(new MethodExpressionParser());
		var parsedExpression = (Body)programType.Methods[0].GetBodyAndParseIfNeeded();
		Assert.That(parsedExpression.ReturnType.Name, Is.EqualTo(Base.Number));
		Assert.That(parsedExpression.Expressions[1], Is.TypeOf(typeof(For)));
		Assert.That(((For)parsedExpression.Expressions[1]).Value.ToString(),
			Is.EqualTo("Range(0, number)"));
	}

	[Test]
	public void ErrorExpressionIsNotAnIterator()
	{
		var programType = new Type(type.Package,
				new TypeLines(nameof(ErrorExpressionIsNotAnIterator), "has number", "LogError Number", "\tconstant error = Error \"Process Failed\"",
					"\tfor error", "\t\tvalue")).
			ParseMembersAndMethods(new MethodExpressionParser());
		Assert.That(() => programType.Methods[0].GetBodyAndParseIfNeeded(),
			Throws.InstanceOf<For.ExpressionTypeIsNotAnIterator>());
	}

	[TestCase("error.Stacktraces", nameof(IterateErrorTypeMembers) + "StackTrace")]
	[TestCase("error.Text", nameof(IterateErrorTypeMembers) + "Text")]
	public void IterateErrorTypeMembers(string forExpressionText, string testName)
	{
		var programType = new Type(type.Package,
				new TypeLines(testName, "has number", "LogError Number", "\tconstant error = Error \"Process Failed\"",
					$"\tfor {forExpressionText}", "\t\tvalue")).
			ParseMembersAndMethods(new MethodExpressionParser());
		var parsedExpression = (Body)programType.Methods[0].GetBodyAndParseIfNeeded();
		Assert.That(parsedExpression.Expressions[1], Is.TypeOf(typeof(For)));
		Assert.That(((For)parsedExpression.Expressions[1]).Value.ToString(),
			Is.EqualTo(forExpressionText));
	}

	[Test]
	public void IterateNameType()
	{
		var programType = new Type(type.Package,
				new TypeLines(nameof(IterateNameType), "has number", "LogError Number", "\tconstant name = Name(\"Strict\")",
					"\tfor name", "\t\tvalue")).
			ParseMembersAndMethods(new MethodExpressionParser());
		var parsedExpression = (Body)programType.Methods[0].GetBodyAndParseIfNeeded();
		Assert.That(parsedExpression.Expressions[1], Is.TypeOf(typeof(For)));
		Assert.That(((For)parsedExpression.Expressions[1]).Value.ToString(), Is.EqualTo("name"));
	}

	[Test]
	public void AllowNestedForWithSameIndentation() =>
		Assert.That(
			((For)ParseExpression(
				"for firstIndex in Range(1, 10)",
				"for secondIndex in Range(1, 10)",
				"\tlog.Write(firstIndex)",
				"\tlog.Write(secondIndex)")).ToString(),
			Is.EqualTo(
				"for firstIndex in Range(1, 10)\n\tfor secondIndex in Range(1, 10)\n\tlog.Write(firstIndex)\r\nlog.Write(secondIndex)"));

	[Test]
	public void MissingBodyInNestedFor() =>
		Assert.That(() => ParseExpression(
				"for Range(2, 5)",
				"for index in Range(1, 10)"),
			Throws.InstanceOf<For.MissingInnerBody>()!);

	[TestCase(
		"WithParameter", "element in (1, 2, 3, 4)",
		"has log",
		"LogError Number",
		"\tfor element in (1, 2, 3, 4)",
		"\t\tlog.Write(element)")]
	[TestCase(
		"WithList", "element in elements",
		"has log",
		"LogError(elements Numbers) Number",
		"\tfor element in elements",
		"\t\tlog.Write(element)")]
	[TestCase(
		"WithListTexts", "element in texts",
		"has log",
		"LogError(texts) Number",
		"\tfor element in texts",
		"\t\tlog.Write(element)")]
	public void AllowCustomVariablesInFor(string testName, string expected, params string[] code)
	{
		var programType =
			new Type(type.Package, new TypeLines(nameof(AllowCustomVariablesInFor) + testName, code)).
				ParseMembersAndMethods(new MethodExpressionParser());
		var parsedExpression = (For)programType.Methods[0].GetBodyAndParseIfNeeded();
		Assert.That(parsedExpression.Value.ToString(), Is.EqualTo(expected));
	}

	[TestCase(
				// @formatter:off
				"WithNumbers",
				"has log",
				"LogError(numbers) Number",
				"\tfor row, column in numbers",
				"\t\tlog.Write(column)")]
		[TestCase(
			"WithTexts",
				"has log",
				"LogError(texts) Number",
				"\tfor row, column in texts",
				"\t\tlog.Write(column)")]
	public void ParseForExpressionWithMultipleVariables(string testName, params string[] code)
	{
		var programType = new Type(type.Package, new TypeLines(nameof(ParseForExpressionWithMultipleVariables) + testName, code
				)).ParseMembersAndMethods(new MethodExpressionParser());
		Assert.That(programType.Methods[0].GetBodyAndParseIfNeeded(), Is.InstanceOf<For>());
	}
}