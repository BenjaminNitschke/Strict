using NUnit.Framework;

namespace Strict.Language.Expressions.Tests;

public sealed class IfTests : TestExpressions
{
	[Test]
	public void MissingCondition() =>
		Assert.That(() => ParseExpression("if"),
			Throws.InstanceOf<If.MissingCondition>().With.Message.
				Contains(@"TestPackage\dummy.strict:line 2"));

	[Test]
	public void InvalidCondition() =>
		Assert.That(() => ParseExpression("if 5", "\treturn 0"),
			Throws.InstanceOf<If.InvalidCondition>());

	[Ignore(
		"TODO: Delete; This won't happen as child body return types are first validated with method return type")]
	[Test]
	public void ReturnTypeOfThenAndElseMustHaveMatchingType() =>
		Assert.That(
			() => ParseExpression("if 5 is 6", "\treturn 8", "else", "\treturn \"hello\"").ReturnType,
			Throws.InstanceOf<If.ReturnTypeOfThenAndElseMustHaveMatchingType>());

	[Test]
	public void ReturnTypeOfThenAndElseIsNumberAndCountIsValid() =>
		Assert.That(
			new Method(type, 0, this,
				new[]
				{
					"ReturnMethod Number", "	if bla is 5", "		return Count(0)", "	else", "		return 5"
				}).GetBodyAndParseIfNeeded().ReturnType, Is.EqualTo(type.GetType(Base.Number)));

	[Test]
	public void ReturnTypeOfThenAndElseIsCountAndCharacterIsValid() =>
		Assert.That(
			new Method(type, 0, this,
				new[]
				{
					"ReturnMethod Number", "	if bla is 5", "		return Count(0)", "	else",
					"		return Character(5)"
				}).GetBodyAndParseIfNeeded().ReturnType, Is.EqualTo(type.GetType(Base.Number)));

	[Test]
	public void ParseInvalidSpaceAfterElseIsNotAllowed() =>
		Assert.That(() => ParseExpression("else "),
			Throws.InstanceOf<Type.ExtraWhitespacesFoundAtEndOfLine>());

	[Test]
	public void ParseJustElseIsNotAllowed() =>
		Assert.That(() => ParseExpression("else"),
			Throws.InstanceOf<If.UnexpectedElse>().With.Message.Contains(@"at Run in "));

	[Test]
	public void ParseIncompleteThen() =>
		Assert.That(() => ParseExpression("if bla is 5"), Throws.InstanceOf<If.MissingThen>());

	[Test]
	public void MissingThen() =>
		Assert.That(() => ParseExpression("if bla is 5", "Run"), Throws.InstanceOf<If.MissingThen>());

	[Test]
	public void ParseIf() =>
		Assert.That(ParseExpression("if bla is 5", "\tlog.Write(\"Hey\")"),
			Is.EqualTo(new If(GetCondition(), GetThen())));

	[Test]
	public void ParseIfNot() =>
		Assert.That(ParseExpression("if bla is not 5", "\tlog.Write(\"Hey\")"),
			Is.EqualTo(new If(GetCondition(true), GetThen())));

	[Test]
	public void InvalidIsNotUsageOnDifferentType() =>
		Assert.That(() => ParseExpression("if bla is not \"blu\"", "\tlog.Write(\"Hey\")"),
			Throws.InstanceOf<Type.ArgumentsDoNotMatchMethodParameters>().With.Message.
				Contains("blu"));

	[Test]
	public void ParseMissingElseExpression() =>
		Assert.That(() => ParseExpression("if bla is 5", "\tRun", "else"),
			Throws.InstanceOf<If.UnexpectedElse>().With.Message.
				Contains(@"TestPackage\dummy.strict:line 4"));

	[Test]
	public void ParseIfElse() =>
		Assert.That(ParseExpression("if bla is 5", "\tlog.Write(\"Hey\")", "else", "\tRun"),
			Is.EqualTo(new If(GetCondition(), GetThen(), new MethodCall(method))).And.Not.
				EqualTo(new If(GetCondition(), GetThen())));

	private MethodCall GetThen() =>
		new(member.Type.Methods[0], new MemberCall(null, member),
			new Expression[] { new Text(type, "Hey") });

	[Test]
	public void ReturnGetHashCode()
	{
		var ifExpression = (If)ParseExpression("if bla is 5", "\tRun");
		Assert.That(ifExpression.GetHashCode(),
			Is.EqualTo(ifExpression.Condition.GetHashCode() ^ ifExpression.Then.GetHashCode()));
	}

	[Test]
	public void MissingElseExpression() =>
		Assert.That(() => ParseExpression("let result = true ? true"),
			Throws.InstanceOf<If.MissingElseExpression>());

	[Test]
	public void InvalidConditionInConditionalExpression() =>
		Assert.That(() => ParseExpression("let result = 5 ? true"),
			Throws.InstanceOf<UnknownExpression>());

	[Test]
	public void ReturnTypeOfConditionalThenAndElseMustHaveMatchingType() =>
		Assert.That(() => ParseExpression("let result = true ? true else 5"),
			Throws.InstanceOf<If.ReturnTypeOfThenAndElseMustHaveMatchingType>());

	[TestCase("let result = true ? true else false")]
	[TestCase("let result = false ? \"Yes\" else \"No\"")]
	[TestCase("let result = 5 is 5 ? (1, 2) else (3, 4)")]
	[TestCase("let result = 5 + (false ? 1 else 2)")]
	[TestCase("let result = 5 is not 4 ? (1, 2) else (3, 4)")]
	public void ValidConditionalExpressions(string code)
	{
		var expression = ParseExpression(code);
		Assert.That(expression, Is.InstanceOf<Assignment>()!);
		var assignment = expression as Assignment;
		Assert.That(assignment?.Value, Is.InstanceOf<If>().Or.InstanceOf<Binary>()!);
	}

	[Test]
	public void ConditionalExpressionsCannotBeNested() =>
		Assert.That(() => ParseExpression("let result = true ? true else (5 is 5 ? false else true)"),
			Throws.InstanceOf<If.ConditionalExpressionsCannotBeNested>());

	[TestCase("log.Write(true ? \"Yes\" else \"No\")")]
	[TestCase("log.Write(true ? \"Yes\" + \"text\" else \"No\")")]
	[TestCase("log.Write(\"Result\" + (true ? \"Yes\" else \"No\"))")]
	[TestCase("let something = 5 is 5 ? false else true")]
	[TestCase("6 is 5 ? true else false")]
	public void ConditionalExpressionsAsPartOfOtherExpression(string code) =>
		Assert.That(ParseExpression(code).ToString(), Is.EqualTo(code));

	[Test]
	public void ReturnTypeOfThenMustMatchMethodReturnType()
	{
		// @formatter:off
		var program = new Type(new Package(nameof(IfTests)),
			new TypeLines(nameof(ReturnTypeOfThenMustMatchMethodReturnType),
				"has log",
				"Run Text",
				"	if 5 is 5",
				"		let file = File(\"test.txt\")",
				"		return 5")).ParseMembersAndMethods(new MethodExpressionParser());
		Assert.That(() => program.Methods[0].GetBodyAndParseIfNeeded(), Throws.InstanceOf<Body.ChildBodyReturnTypeMustMatchMethodReturnType>().With.Message.Contains("Child body return type: TestPackage.Number is not matching with Parent return type: TestPackage.Text in method line: 3"));
	}

	[Test]
	public void ReturnTypeOfElseMustMatchMethodReturnType()
	{
		var program = new Type(new Package(nameof(IfTests)),
			new TypeLines(nameof(ReturnTypeOfElseMustMatchMethodReturnType),
				"has log",
				"Run Text",
				"	if 5 is 5",
				"		let file = File(\"test.txt\")",
				"		return \"Hello\"",
				"	else",
				"		return true")).ParseMembersAndMethods(new MethodExpressionParser());
		Assert.That(() => program.Methods[0].GetBodyAndParseIfNeeded(), Throws.InstanceOf<Body.ChildBodyReturnTypeMustMatchMethodReturnType>().With.Message.Contains("Child body return type: TestPackage.Boolean is not matching with Parent return type: TestPackage.Text in method line: 5"));
	}

	[Test]
	public void ThenReturnsImplementedTypeOfMethodReturnType()
	{
		var program = new Type(new Package(nameof(IfTests)),
			new TypeLines(nameof(ThenReturnsImplementedTypeOfMethodReturnType),
				"has log",
				"Run Number",
				"	if 5 is 5",
				"		let file = File(\"test.txt\")",
				"		return Count(5)",
				"	6")).ParseMembersAndMethods(new MethodExpressionParser());
		Assert.That(((Body) program.Methods[0].GetBodyAndParseIfNeeded()).children[0].ReturnType.ToString(), Is.EqualTo("TestPackage.Number"));
	}

	[Test]
	public void MultiLineThenAndElseWithMatchingMethodReturnType()
	{
		var program = new Type(new Package(nameof(IfTests)),
			new TypeLines(nameof(MultiLineThenAndElseWithMatchingMethodReturnType),
				"has log",
				"ValidRun Text",
				"	if 5 is 5",
				"		let file = File(\"test.txt\")",
				"		return \"Hello\"",
				"	else",
				"		return \"Hi\"",
				"	\"don't matter\"")).ParseMembersAndMethods(new MethodExpressionParser());
		var body = (Body) program.Methods[0].GetBodyAndParseIfNeeded();
		Assert.That(body.ReturnType.ToString(), Is.EqualTo("TestPackage.Text"));
		Assert.That(body.children[0].ReturnType.ToString(), Is.EqualTo("TestPackage.Text"));
		Assert.That(body.children[1].ReturnType.ToString(), Is.EqualTo("TestPackage.Text"));
	}
}