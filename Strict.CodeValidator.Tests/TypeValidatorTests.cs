﻿using Strict.Language;
using Strict.Language.Expressions;
using Strict.Language.Tests;
using Type = Strict.Language.Type;

namespace Strict.CodeValidator.Tests;

public sealed class TypeValidatorTests
{
	[SetUp]
	public void CreatePackageAndParser()
	{
		package = new TestPackage();
		parser = new MethodExpressionParser();
	}

	private Package package = null!;
	private ExpressionParser parser = null!;

	[Test]
	public void ValidateUnusedMember() =>
		Assert.That(
			() => new TypeValidator(new[]
			{
				ParseTypeMethods(CreateType(nameof(ValidateUnusedMember),
					new[]
					{
						"has unused Number",
						"Run(methodInput Number)",
						"\tconstant result = 5 + methodInput",
						"\tresult"
					}))
			}).Validate(), Throws.InstanceOf<MemberValidator.UnusedMemberMustBeRemoved>()!.With.Message.Contains("unused")!);

	private static Type ParseTypeMethods(Type type)
	{
		foreach (var method in type.Methods)
			method.GetBodyAndParseIfNeeded();
		return type;
	}

	private Type CreateType(string typeName, string[] code) =>
		new Type(package, new TypeLines(typeName,
			code)).ParseMembersAndMethods(parser);

	[Test]
	public void ProperlyUsedMemberShouldBeAllowed() =>
		Assert.DoesNotThrow(
			() => new TypeValidator(new[]
			{
				ParseTypeMethods(CreateType(nameof(ProperlyUsedMemberShouldBeAllowed),
					new[]
					{
						"has usedMember Number",
						"Run(methodInput Number)",
						"\tconstant result = usedMember + methodInput",
						"\tresult"
					}))
			}).Validate());

	[Test]
	public void ValidateTypeHasTooManyDependencies() =>
		Assert.That(() => new TypeValidator(new[]
			{
				ParseTypeMethods(CreateType(nameof(ValidateTypeHasTooManyDependencies),
						// @formatter:off
					new[]
					{
						"has number",
						"has text",
						"has boolean",
						"has character",
						"has input Text",
						"Run(methodInput Number)",
						"\tif boolean",
						"\t\treturn text + input + number + methodInput + character",
						"\t0"
						// @formatter:on
					}))
			}).Validate(),
			Throws.InstanceOf<TypeValidator.TypeHasTooManyDependencies>()!.With.Message.Contains(
				"number TestPackage.Number, text TestPackage.Text, boolean TestPackage.Boolean, " +
				"character TestPackage.Character, input TestPackage.Text"));

	[Test]
	public void ValidateTypeHasTooManyDependenciesFromMethod() =>
		Assert.That(() => new TypeValidator(new[]
			{
				ParseTypeMethods(CreateType(nameof(ValidateTypeHasTooManyDependenciesFromMethod), new[]
				{
						// @formatter:off
						"has number",
						"from(number, text, boolean, input Text)",
						"\tvalue",
						"Run(methodInput Number)",
						"\tif boolean",
						"\t\treturn text + input + number + methodInput + character",
						"\t0"
					// @formatter:on
				}))
			}).Validate(),
			Throws.InstanceOf<Method.MethodParameterCountMustNotExceedThree>()!.With.Message.Contains(
				"Type TestPackage.ValidateTypeHasTooManyDependenciesFromMethod constructor method has parameters count 4 but limit is 3"));

	[Test]
	public void VariableHidesMemberUseDifferentName() =>
		Assert.That(() => new TypeValidator(new[]
			{
				ParseTypeMethods(CreateType(nameof(VariableHidesMemberUseDifferentName), new[]
				{
						// @formatter:off
						"has input Number",
						"FirstMethod(methodInput Number) Number",
						"\tconstant something = 5",
						"\tmethodInput + something",
						"SecondMethod(methodInput Number) Number",
						"\tconstant second = 5",
						"\tmethodInput + second",
						"Run(methodInput Number)",
						"\tconstant input = 5",
						"\tmethodInput + input"
					// @formatter:on
				}))
			}).Validate(),
			Throws.InstanceOf<MethodValidator.VariableHidesMemberUseDifferentName>()!.With.Message.
				Contains("Method name Run, Variable name input"));

	[Test]
	public void ParameterHidesMemberUseDifferentName() =>
		Assert.That(() => new TypeValidator(new[]
			{
				ParseTypeMethods(CreateType(nameof(VariableHidesMemberUseDifferentName), new[]
				{
						// @formatter:off
						"has input Number",
						"FirstMethod(input Number) Number",
						"\tconstant something = 5",
						"\tinput + something",
						"SecondMethod(methodInput Number) Number",
						"\tconstant second = 5",
						"\tmethodInput + second"
					// @formatter:on
				}))
			}).Validate(),
			Throws.InstanceOf<MethodValidator.ParameterHidesMemberUseDifferentName>()!.With.Message.
				Contains("Method name FirstMethod, Parameter name input"));
}