﻿using System;

namespace Strict.Language.Expressions;

/// <summary>
/// Parses for loop expressions. Usually loop would have an implicit variable if not explicitly given anything,
/// so the variable is parsed with the first value of the iterable,
/// e.g for list the first element of the list or for range from 0
/// If explicit variable is given, the variable is added in the body, similarly to implicit index/value variables.
/// </summary>
public sealed class For : Expression
{
	private For(Expression value, Expression body) : base(value.ReturnType)
	{
		Value = value;
		Body = body;
	}

	public Expression Value { get; }
	public Expression Body { get; }
	public override int GetHashCode() => Value.GetHashCode();
	public override string ToString() => $"for {Value}\n\t{Body}";
	public override bool Equals(Expression? other) => other is For a && Equals(Value, a.Value);
	private const string ValueName = "value";
	private const string IndexName = "index";
	private const string InName = "in ";
	private static bool HasIn(ReadOnlySpan<char> line) => line.Contains(InName, StringComparison.Ordinal);

	public static Expression? TryParse(Body body, ReadOnlySpan<char> line)
	{
		if (!line.StartsWith(Keyword.For, StringComparison.Ordinal))
			return null;
		if (line.Length <= Keyword.For.Length)
			throw new MissingExpression(body);
		var innerBody = body.FindCurrentChild() ??
			(TryGetInnerForAsBody(body) ?? throw new MissingInnerBody(body));
		if (line.Contains(IndexName, StringComparison.Ordinal))
			throw new IndexIsReserved(body);
		return ParseFor(body, line, innerBody);
	}

	private static Body? TryGetInnerForAsBody(Body body)
	{
		if (IsLastLine(body) || !IsNextLineStartsWithFor(body))
			return null;
		var currentLineNumber = body.ParsingLineNumber++;
		var child = body.FindCurrentChild();
		return child == null
			? null
			: body.GetInnerBodyAndUpdateHierarchy(currentLineNumber, child);
	}

	private static bool IsLastLine(Body body) => body.ParsingLineNumber + 1 == body.LineRange.End.Value;

	private static bool IsNextLineStartsWithFor(Body body) =>
		body.GetLine(body.ParsingLineNumber + 1).TrimStart().
			StartsWith(Keyword.For, StringComparison.Ordinal);

	public sealed class MissingInnerBody : ParsingFailed
	{
		public MissingInnerBody(Body body) : base(body) { }
	}

	private static Expression ParseFor(Body body, ReadOnlySpan<char> line, Body innerBody)
	{
		if (!HasIn(line) && line[^1] == ')')
			return ParseWithImplicitVariable(body, line, innerBody);
		CheckForUnidentifiedIterable(body, line);
		return ParseWithExplicitVariable(body, line, innerBody);
	}

	private static Expression ParseWithExplicitVariable(Body body,
		ReadOnlySpan<char> line, Body innerBody)
	{
		var variableName = FindIterableName(line);
		AddVariableIfDoesNotExist(body, line, variableName);
		var variableValue = body.FindVariableValue(variableName);
		if (variableValue is { IsMutable: false } && HasIn(line))
			throw new ImmutableIterator(body);
		var forExpression = body.Method.ParseExpression(body, line[4..]);
		if (HasIn(line))
			CheckForIncorrectMatchingTypes(body, variableName, forExpression);
		else
			AddImplicitVariables(body, line, innerBody);
		if (!GetIteratorType(forExpression).IsIterator)
			throw new ExpressionTypeIsNotAnIterator(body, forExpression.ReturnType.Name,
				line[4..].ToString());
		return new For(forExpression, innerBody.Parse());
	}

	public sealed class ExpressionTypeIsNotAnIterator : ParsingFailed
	{
		public ExpressionTypeIsNotAnIterator(Body body, string typeName, string line) : base(body,
			$"Type {typeName} in line " + line) { }
	}

	private static void AddVariableIfDoesNotExist(Body body, ReadOnlySpan<char> line, ReadOnlySpan<char> variableName)
	{
		var variableValue = body.FindVariableValue(variableName);
		if (variableValue != null)
			return;
		if (body.Method.Type.FindMember(variableName.ToString()) == null)
		{
			variableValue = body.Method.ParseExpression(body, GetVariableExpressionValue(body, line));
			variableValue.IsMutable = true;
			body.AddVariable(variableName.ToString(), variableValue);
		}
	}

	private static void CheckForIncorrectMatchingTypes(Body body, ReadOnlySpan<char> variableName,
		Expression forValueExpression)
	{
		var mutableValue = body.FindVariableValue(variableName);
		var iteratorType = GetIteratorType(forValueExpression);
		if (iteratorType is GenericTypeImplementation { IsIterator: true } genericType)
			iteratorType = genericType.ImplementationTypes[0];
		if ((iteratorType.Name != Base.Range || mutableValue?.ReturnType.Name != Base.Number)
			&& iteratorType.Name != mutableValue?.ReturnType.Name)
			throw new IteratorTypeDoesNotMatchWithIterable(body);
	}

	private static Type GetIteratorType(Expression forValueExpression) =>
		forValueExpression is Binary binary
			? binary.Arguments[0].ReturnType
			: forValueExpression.ReturnType;

	private static void CheckForUnidentifiedIterable(Body body, ReadOnlySpan<char> line)
	{
		if (body.FindVariableValue(FindIterableName(line)) == null &&
			body.Method.Type.FindMember(FindIterableName(line).ToString()) == null && line[^1] != ')')
			throw new UnidentifiedIterable(body);
	}

	private static Expression ParseWithImplicitVariable(Body body, ReadOnlySpan<char> line,
		Body innerBody)
	{
		if (body.FindVariableValue(IndexName) != null)
			throw new DuplicateImplicitIndex(body);
		AddImplicitVariables(body, line, innerBody);
		return new For(body.Method.ParseExpression(body, line[4..]), innerBody.Parse());
	}

	private static void AddImplicitVariables(Body body, ReadOnlySpan<char> line, Body innerBody)
	{
		innerBody.AddVariable(IndexName, new Number(body.Method, 0));
		var variableValue = innerBody.Method.ParseExpression(innerBody, GetVariableExpressionValue(body, line));
		variableValue.IsMutable = true;
		innerBody.AddVariable(ValueName, variableValue);
	}

	private static string GetVariableExpressionValue(Body body, ReadOnlySpan<char> line)
	{
		if (line.Contains("Range", StringComparison.Ordinal))
			return $"{GetRangeExpression(line)}.Start";
		var iterableName = FindIterableName(line);
		var variable = body.FindVariableValue(iterableName)?.ReturnType ?? body.Method.Type.FindMember(iterableName.ToString())?.Type;
		var value = iterableName[^1] == ')'
			? iterableName[1..iterableName.IndexOf(',')].ToString()
			: variable != null && variable.IsIterator
				? $"{iterableName}(0)"
				: $"{iterableName}";
		return value;
	}

	private static ReadOnlySpan<char> GetRangeExpression(ReadOnlySpan<char> line) =>
		line[line.LastIndexOf('R')..(line.LastIndexOf(')') + 1)];

	private static ReadOnlySpan<char> FindIterableName(ReadOnlySpan<char> line) =>
		line.Contains(InName, StringComparison.Ordinal)
			? line[4..(line.LastIndexOf(InName) - 1)]
			: line.Contains('.')
				? line[(line.IndexOf(' ') + 1)..line.IndexOf('.')]
				: line[(line.IndexOf(' ') + 1)..];

	public sealed class MissingExpression : ParsingFailed
	{
		public MissingExpression(Body body) : base(body) { }
	}

	public sealed class IndexIsReserved : ParsingFailed
	{
		public IndexIsReserved(Body body) : base(body) { }
	}

	public sealed class DuplicateImplicitIndex : ParsingFailed
	{
		public DuplicateImplicitIndex(Body body) : base(body) { }
	}

	public sealed class UnidentifiedIterable : ParsingFailed
	{
		public UnidentifiedIterable(Body body) : base(body) { }
	}

	public sealed class ImmutableIterator : ParsingFailed
	{
		public ImmutableIterator(Body body) : base(body) { }
	}

	public sealed class IteratorTypeDoesNotMatchWithIterable : ParsingFailed
	{
		public IteratorTypeDoesNotMatchWithIterable(Body body) : base(body) { }
	}
}