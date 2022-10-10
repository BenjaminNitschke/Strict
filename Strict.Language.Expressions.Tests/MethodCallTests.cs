using System.Collections.Generic;
using NUnit.Framework;
using static Strict.Language.Type;

namespace Strict.Language.Expressions.Tests;

// ReSharper disable once ClassTooBig
public sealed class MethodCallTests : TestExpressions
{
	[SetUp]
	public void AddComplexMethods()
	{
		((List<Method>)type.Methods).Add(new Method(type, 0, this,
			new[] { "complexMethod(numbers, add Number) Number", "\t1" }));
		((List<Method>)type.Methods).Add(new Method(type, 0, this,
			new[] { "complexMethod(texts) Texts", "\t1" }));
	}

	[Test]
	public void ParseLocalMethodCall() =>
		ParseAndCheckOutputMatchesInput("Run", new MethodCall(type.Methods[0]));

	[Test]
	public void ParseCallWithArgument() =>
		ParseAndCheckOutputMatchesInput("log.Write(bla)",
			new MethodCall(member.Type.Methods[0], new MemberCall(null, member),
				new[] { new MemberCall(null, bla) }));

	[Test]
	public void ParseCallWithTextArgument() =>
		ParseAndCheckOutputMatchesInput("log.Write(\"Hi\")",
			new MethodCall(member.Type.Methods[0], new MemberCall(null, member),
				new[] { new Text(type, "Hi") }));

	[Test]
	public void ParseWithMissingArgument() =>
		Assert.That(() => ParseExpression("log.Write"),
			Throws.InstanceOf<Type.ArgumentsDoNotMatchMethodParameters>().With.Message.StartsWith(
				"No arguments does not match these method(s):\nWrite(text TestPackage.Text)\nWrite(number TestPackage.Number)\nWrite(mutable TestPackage.Mutable)"));

	[Test]
	public void ParseWithTooManyArguments() =>
		Assert.That(() => ParseExpression("log.Write(1, 2)"),
			Throws.InstanceOf<Type.ArgumentsDoNotMatchMethodParameters>().With.Message.
				StartsWith("Arguments: 1 TestPackage.Number, 2 TestPackage.Number do not match"));

	[Test]
	public void ParseWithInvalidExpressionArguments() =>
		Assert.That(() => ParseExpression("log.Write(g9y53)"),
			Throws.InstanceOf<UnknownExpressionForArgument>().With.Message.
				StartsWith("g9y53 (argument 0)"));

	[Test]
	public void EmptyBracketsAreNotAllowed() =>
		Assert.That(() => ParseExpression("log.NotExisting()"),
			Throws.InstanceOf<List.EmptyListNotAllowed>());

	[Test]
	public void MethodNotFound() =>
		Assert.That(() => ParseExpression("log.NotExisting"),
			Throws.InstanceOf<MemberOrMethodNotFound>());

	[Test]
	public void ArgumentsDoNotMatchMethodParameters() =>
		Assert.That(() => ParseExpression("Character(\"Hi\")"),
			Throws.InstanceOf<Type.ArgumentsDoNotMatchMethodParameters>());

	[Test]
	public void ParseCallWithUnknownMemberCallArgument() =>
		Assert.That(() => ParseExpression("log.Write(log.unknown)"),
			Throws.InstanceOf<MemberOrMethodNotFound>().With.Message.
				StartsWith("unknown in TestPackage.Log"));

	[Test]
	public void MethodCallMembersMustBeWords() =>
		Assert.That(() => ParseExpression("g9y53.Write"), Throws.InstanceOf<MemberOrMethodNotFound>());

	[Test]
	public void UnknownExpressionForArgumentException() =>
		Assert.That(() => ParseExpression("complexMethod((\"1 + 5\" + \"5\"))"),
			Throws.InstanceOf<ArgumentsDoNotMatchMethodParameters>().With.Message.
				StartsWith("Argument: \"1 + 5\" + \"5\" "));

	[Test]
	public void ListTokensAreNotSeparatedByCommaException() =>
		Assert.That(() => ParseExpression("complexMethod((\"1 + 5\" 5, \"5 + 5\"))"),
			Throws.InstanceOf<ListTokensAreNotSeparatedByComma>());

	[Test]
	public void SimpleFromMethodCall() =>
		Assert.That(ParseExpression("Character(7)"),
			Is.EqualTo(CreateFromMethodCall(type.GetType(Base.Character), new Number(type, 7))));

	[TestCase("Count(5)")]
	[TestCase("Character(5)")]
	[TestCase("Count(5).Increment")]
	[TestCase("Count(5).Floor")]
	[TestCase("Range(0, 10)")]
	[TestCase("Range(0, 10).Length")]
	public void FromExample(string fromMethodCall) =>
		Assert.That(ParseExpression(fromMethodCall).ToString(), Is.EqualTo(fromMethodCall));

	[Test]
	public void MakeSureMutableHasAllTheTypesOfItsChild()
	{
		var expression = ParseExpression("Mutable(7)");
		Assert.That(expression.ReturnType.Methods.Count,
			Is.EqualTo(((Mutable)expression).DataReturnType.Methods.Count + 2));
	}

	[Test]
	public void FromExampleFailsOnImproperParameters() =>
		Assert.That(() => ParseExpression("Range(1, 2, 3, 4)"),
			Throws.InstanceOf<NoMatchingMethodFound>());

	[TestCase("complexMethod((1), 2)")]
	[TestCase("complexMethod((1, 2, 3) + (4, 5), 7)")]
	[TestCase("complexMethod((1, 2, 3) + (4, 5), complexMethod((1, 2, 3), 4))")]
	[TestCase("complexMethod((\"1 + 5\", \"5 + 5\"))")]
	public void FindRightMethodCall(string methodCall) =>
		Assert.That(ParseExpression(methodCall).ToString(), Is.EqualTo(methodCall));

	[Test]
	public void IsMethodPublic() =>
		Assert.That((ParseExpression("Run") as MethodCall)?.Method.IsPublic, Is.True);

	[Test]
	public void ValueMustHaveCorrectType()
	{
		var program = new Type(type.Package, new TypeLines(
				nameof(ValueMustHaveCorrectType),
				"has log",
				"has Number",
				$"Dummy(dummy Number) {nameof(ValueMustHaveCorrectType)}",
				"\tlet result = value",
				"\tresult")).
			ParseMembersAndMethods(new MethodExpressionParser());
		var body = (Body)program.Methods[0].GetBodyAndParseIfNeeded();
		Assert.That(body.FindVariableValue("value")?.ReturnType, Is.EqualTo(program));
	}

	[Test]
	public void CanAccessThePropertiesOfValue()
	{
		var program = new Type(type.Package, new TypeLines(
				nameof(CanAccessThePropertiesOfValue),
				"has log",
				"has Number",
				"has myMember Text",
				"Dummy(dummy Number) Text",
				"\tlet result = value.myMember",
				"\tresult")).
			ParseMembersAndMethods(new MethodExpressionParser());
		var body = (Body)program.Methods[0].GetBodyAndParseIfNeeded();
		Assert.That(body.FindVariableValue("value")?.ReturnType, Is.EqualTo(program));
		Assert.That(body.FindVariableValue("result")?.ReturnType.Name, Is.EqualTo("Text"));
	}

	[TestCase("ProgramWithHas",
		"has numbers",
		"has texts",
		"Dummy",
		"\tlet instanceWithNumbers = ProgramWithHas(1, 2, 3)",
		"\tlet instanceWithTexts = ProgramWithHas(\"1\", \"2\", \"3\")")]
	[TestCase("ProgramWithImplement",
		"implement Numbers",
		"implement Texts",
		"Dummy",
		"\tlet instanceWithNumbers = ProgramWithImplement(1, 2, 3)",
		"\tlet instanceWithTexts = ProgramWithImplement(\"1\", \"2\", \"3\")")]
	public void ParseConstructorCallWithList(string programName, params string[] code)
	{
		var program = new Type(type.Package, new TypeLines(
				programName,
				code)).
			ParseMembersAndMethods(new MethodExpressionParser());
		var body = (Body)program.Methods[0].GetBodyAndParseIfNeeded();
		Assert.That(((MethodCall)((Assignment)body.Expressions[0]).Value).Method.Parameters[0].Name,
			Is.EqualTo("numbers"));
		Assert.That(((MethodCall)((Assignment)body.Expressions[1]).Value).Method.Parameters[0].Name,
			Is.EqualTo("texts"));
	}

	[Test]
	public void TypeImplementsGenericTypeWithLength()
	{
		var program = new Type(type.Package,
			new TypeLines(nameof(TypeImplementsGenericTypeWithLength),
				"has log", //unused member should be removed later when we allow class without members
				"GetLengthSquare(type HasLength) Number",
				"\ttype.Length * type.Length",
				"Dummy",
				"\tlet countOfFive = Count(5)",
				"\tlet lengthSquare = GetLengthSquare(countOfFive)")).ParseMembersAndMethods(new MethodExpressionParser());
		Assert.That(program.Methods[1].GetBodyAndParseIfNeeded().ToString(),
			Is.EqualTo("let countOfFive = Count(5)\r\nlet lengthSquare = GetLengthSquare(countOfFive)"));
	}

	[Test]
	public void MutableCanUseChildMethods()
	{
		var program = new Type(type.Package,
			new TypeLines(nameof(MutableCanUseChildMethods),
				"has log",
				"Dummy Number",
				"\tlet mutableNumber = Mutable(5)",
				"\tmutableNumber + 10")).ParseMembersAndMethods(new MethodExpressionParser());
		Assert.That(program.Methods[0].GetBodyAndParseIfNeeded().ToString(),
			Is.EqualTo("let mutableNumber = 5\r\nmutableNumber + 10"));
	}

	[Test]
	public void ConstructorCallWithMethodCall()
	{
		var program = new Type(type.Package,
			new TypeLines("ArithmeticFunction",
				"has numbers",
				"from(first Number, second Number)",
				"\tnumbers = (first, second)",
				"Calculate(text) Number",
				"\tArithmeticFunction(10, 5).Calculate(\"add\")",
				"\t1")).ParseMembersAndMethods(new MethodExpressionParser());
		program.Methods[1].GetBodyAndParseIfNeeded();
	}

	[Test]
	public void NestedMethodCall()
	{
		var program = new Type(type.Package,
				new TypeLines(nameof(NestedMethodCall), "has log", "Run",
					"\tFile(\"fileName\").Write(\"someText\")", "\ttrue")).
			ParseMembersAndMethods(new MethodExpressionParser());
		var body = (Body)program.Methods[0].GetBodyAndParseIfNeeded();
		Assert.That(body.Expressions[0], Is.InstanceOf<MethodCall>());
		Assert.That(((MethodCall)body.Expressions[0]).Method.Name, Is.EqualTo("Write"));
		Assert.That(((MethodCall)body.Expressions[0]).Instance?.ToString(),
			Is.EqualTo("File(\"fileName\")"));
	}
}