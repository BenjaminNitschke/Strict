﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Strict.Language.Tests")]

namespace Strict.Language;

/// <summary>
/// Methods are parsed lazily, which speeds up type and package parsing enormously and
/// also provides us with all methods in a type usable in any other method if needed.
/// </summary>
public sealed class Method : Context
{
	public Method(Type type, int typeLineNumber, ExpressionParser parser, IReadOnlyList<string> lines)
		: base(type, GetName(lines[0]))
	{
		TypeLineNumber = typeLineNumber;
		this.parser = parser;
		this.lines = lines;
		ReturnType = ParseParametersAndReturnType(type, lines[0].AsSpan(Name.Length));
		if (lines.Count > 1)
			methodBody = PreParseBody();
	}

	/// <summary>
	/// Simple lexer to just parse the method definition and get all used names and types. Method code
	/// itself is parsed only on demand (when GetBodyAndParseIfNeeded is called) in are more complex
	/// way (Shunting yard/BNF/etc.) and slower. Examples: Run, Run(number), Run returns Text
	/// </summary>
	// ReSharper disable once MethodTooLong
	private static string GetName(ReadOnlySpan<char> firstLine)
	{
		var name = firstLine;
		var isNameIsNotOperator = false;
		for (var i = 0; i < firstLine.Length; i++)
			if (firstLine[i] == '(' || firstLine[i] == ' ')
			{
				name = firstLine[..i];
				if (firstLine.StartsWith("is not(", StringComparison.Ordinal))
				{
					isNameIsNotOperator = true;
					name = firstLine[..(i + 4)];
				}
				break;
			}
		if (!name.IsWord() && !name.IsOperator() && !isNameIsNotOperator)
			throw new NameMustBeAWordWithoutAnySpecialCharactersOrNumbers(name.ToString());
		return name.ToString();
	}

	public int TypeLineNumber { get; }
	private readonly ExpressionParser parser;
	internal readonly IReadOnlyList<string> lines;
	private readonly Body? methodBody;

	private Type ParseParametersAndReturnType(Type type, ReadOnlySpan<char> rest)
	{
		if (rest.Length == 0)
			return GetEmptyReturnType(type);
		if (IsReturnTypeAny(rest))
			throw new MethodReturnTypeAsAnyIsNotAllowed(this, rest.ToString());
		var closingBracketIndex = rest.LastIndexOf(')');
		var gotBrackets = closingBracketIndex > 0;
		return gotBrackets && rest.Length == 2
			? throw new EmptyParametersMustBeRemoved(this)
			: rest[0] == ' ' && !gotBrackets
				? Type.GetType(rest[1..].ToString())
				: rest[0] != '(' == gotBrackets || rest.Length < 2
					? throw new InvalidMethodParameters(this, rest.ToString())
					: !gotBrackets
						? type.GetType(rest[1..].ToString())
						: ParseParameters(type, rest, closingBracketIndex);
	}

	private Type GetEmptyReturnType(Type type) =>
		Name == From
			? type
			: type.GetType(Base.None);

	private static bool IsReturnTypeAny(ReadOnlySpan<char> rest) =>
		rest[0] == ' ' && rest[1..].Equals(Base.Any, StringComparison.Ordinal);

	public sealed class MethodReturnTypeAsAnyIsNotAllowed : ParsingFailed
	{
		public MethodReturnTypeAsAnyIsNotAllowed(Method method, string name) : base(method.Type, 0, name) { }
	}

	private Type ParseParameters(Type type, ReadOnlySpan<char> rest, int closingBracketIndex)
	{
		foreach (var nameAndType in rest[1..closingBracketIndex].
			Split(',', StringSplitOptions.TrimEntries))
		{
			if (char.IsUpper(nameAndType[0]))
				throw new ParametersMustStartWithLowerCase(this);
			var nameAndTypeAsString = nameAndType.ToString();
			if (IsParameterTypeAny(nameAndTypeAsString))
				throw new ParametersWithTypeAnyIsNotAllowed(this, nameAndTypeAsString);
			parameters.Add(new Parameter(type, nameAndTypeAsString));
		}
		return closingBracketIndex + 2 < rest.Length
			? Type.GetType(rest[(closingBracketIndex + 2)..].ToString())
			: GetEmptyReturnType(type);
	}

	private static bool IsParameterTypeAny(string nameAndTypeString) =>
		nameAndTypeString == Base.Any.MakeFirstLetterLowercase() ||
		nameAndTypeString.Contains(" Any");

	public sealed class ParametersWithTypeAnyIsNotAllowed : ParsingFailed
	{
		public ParametersWithTypeAnyIsNotAllowed(Method method, string name) : base(method.Type, 0, name) { }
	}

	public sealed class InvalidMethodParameters : ParsingFailed
	{
		public InvalidMethodParameters(Method method, string rest) : base(method.Type, 0, rest,
			method.Name) { }
	}

	public sealed class EmptyParametersMustBeRemoved : ParsingFailed
	{
		public EmptyParametersMustBeRemoved(Method method) : base(method.Type, 0, "", method.Name) { }
	}

	public sealed class ParametersMustStartWithLowerCase : ParsingFailed
	{
		public ParametersMustStartWithLowerCase(Method method) : base(method.Type, 0, "", method.Name) { }
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Expression ParseLine(Body body, string currentLine) =>
		parser.ParseLineExpression(body, currentLine.AsSpan(body.Tabs));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Expression ParseExpression(Body body, ReadOnlySpan<char> text) =>
		parser.ParseExpression(body, text);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public List<Expression> ParseListArguments(Body body, ReadOnlySpan<char> text) =>
		parser.ParseListArguments(body, text);

	public const string From = "from";

	/// <summary>
	/// Skips the first method declaration line, then counts and removes the tabs from each line.
	/// Also groups all expressions on the same tabs level into bodies. In case a body has only
	/// a single line (which is most often the case), that only expression is used directly.
	/// </summary>
	private Body PreParseBody(int parentTabs = 1, Body? parent = null)
	{
		var body = new Body(this, parentTabs, parent);
		var startLine = methodLineNumber;
		for (; methodLineNumber < lines.Count; methodLineNumber++)
			if (CheckBodyLine(lines[methodLineNumber], body))
				break;
		body.LineRange = new Range(startLine, Math.Min(methodLineNumber, lines.Count));
		return body;
	}

	private int methodLineNumber = 1;

	private bool CheckBodyLine(string line, Body body)
	{
		if (line.Length == 0)
			throw new Type.EmptyLineIsNotAllowed(Type, TypeLineNumber + methodLineNumber);
		var tabs = GetTabs(line);
		if (tabs > body.Tabs)
			PreParseBody(tabs, body);
		CheckIndentation(line, TypeLineNumber + methodLineNumber, tabs);
		return tabs < body.Tabs;
	}

	private static int GetTabs(string line)
	{
		var tabs = 0;
		foreach (var t in line)
			if (t == '\t')
				tabs++;
			else
				break;
		return tabs;
	}

	private void CheckIndentation(string line, int lineNumber, int tabs)
	{
		if (tabs is 0 or > 3)
			throw new InvalidIndentation(Type, lineNumber, line, Name);
		if (char.IsWhiteSpace(line[tabs]))
			throw new Type.ExtraWhitespacesFoundAtBeginningOfLine(Type, lineNumber, line, Name);
		if (char.IsWhiteSpace(line[^1]))
			throw new Type.ExtraWhitespacesFoundAtEndOfLine(Type, lineNumber, line, Name);
	}

	public sealed class InvalidIndentation : ParsingFailed
	{
		public InvalidIndentation(Type type, int lineNumber, string line, string method) : base(type,
			lineNumber, method, line) { }
	}

	public Type Type => (Type)Parent;
	public IReadOnlyList<Parameter> Parameters => parameters;
	private List<Parameter> parameters = new();
	public Type ReturnType { get; private set; }
	public bool IsPublic => char.IsUpper(Name[0]);

	public override Type? FindType(string name, Context? searchingFrom = null) =>
		name == ValueName
			? Type
			: Type.FindType(name, searchingFrom ?? this);

	public const string ValueName = "Value";

	public Expression GetBodyAndParseIfNeeded() =>
		methodBody == null
			? throw new CannotCallBodyOnTraitMethod()
			: methodBody.Expressions.Count > 0
				? methodBody
				: methodBody.Parse();

	public class CannotCallBodyOnTraitMethod : Exception { }

	public override string ToString() =>
		Name + parameters.ToBrackets() + (ReturnType.Name == Base.None
			? ""
			: " " + ReturnType.Name);

	public Method CloneWithImplementation(GenericType typeWithImplementation)
	{
		var clone = (Method)MemberwiseClone();
		clone.ReturnType = ReplaceWithImplementationOrGenericType(clone.ReturnType, typeWithImplementation);
		clone.parameters = new List<Parameter>(parameters);
		for (var index = 0; index < clone.Parameters.Count; index++)
			clone.parameters[index] = clone.parameters[index].CloneWithImplementationType(
				ReplaceWithImplementationOrGenericType(clone.Parameters[index].Type,
					typeWithImplementation));
		return clone;
	}

	private static Type ReplaceWithImplementationOrGenericType(Type type, GenericType typeWithImplementation) =>
		type.Name == Base.Generic
			? typeWithImplementation.Implementation //Number
			: type.IsGeneric
				? typeWithImplementation //ListNumber
				: type;
}