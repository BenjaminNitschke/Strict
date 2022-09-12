﻿using System;

namespace Strict.Language.Expressions;

public sealed class For : Expression
{
	private const string ForName = "for";
	private const string IndexName = "index";
	private const string InName = "in";

	public For(Expression value, Expression body) : base(value.ReturnType)
	{
		Value = value;
		Body = body;
	}

	public Expression Value { get; }
	public Expression Body { get; }
	public override int GetHashCode() => Value.GetHashCode();
	public override string ToString() => $"for {Value}";
	public override bool Equals(Expression? other) => other is For a && Equals(Value, a.Value);

	public static Expression? TryParse(Body body, ReadOnlySpan<char> line)
	{
		if (!line.StartsWith(ForName, StringComparison.Ordinal))
			return null;
		if (line.Length <= ForName.Length)
			throw new MissingExpression(body);
		var innerBody = body.FindCurrentChild();
		if (innerBody == null)
			throw new MissingInnerBody(body);
		if (line.Contains(IndexName, StringComparison.Ordinal))
			throw new UsageOfInferredVariable(body);
		return line.Contains("Range", StringComparison.Ordinal)
			? ParseForRange(body, line, innerBody)
			: ParseFor(body, line, innerBody);
	}

	private static Expression? ParseFor(Body body, ReadOnlySpan<char> line, Body innerBody)
	{
		innerBody.AddVariable(IndexName, new Number(body.Method, 0));
		return new For(body.Method.ParseExpression(body, line[4..]), innerBody.Parse());
	}

	private static Expression ParseForRange(Body body, ReadOnlySpan<char> line, Body innerBody)
	{
		var variableExpressionValue =
			string.Concat(line[line.LastIndexOf('R')..(line.LastIndexOf(')') + 1)], ".Start".AsSpan());
		if (line.Contains(InName, StringComparison.Ordinal) &&
			!line.Contains(IndexName, StringComparison.Ordinal))
		{
			var variableName = line[4..(line.LastIndexOf(InName) - 1)];
			if (body.FindVariableValue(variableName) == null)
				body.AddVariable(variableName.ToString(),
					body.Method.ParseExpression(body, variableExpressionValue));
		}
		innerBody.AddVariable(IndexName, body.Method.ParseExpression(body, variableExpressionValue));
		return new For(body.Method.ParseExpression(body, line[4..]), innerBody.Parse());
	}

	public sealed class MissingExpression : ParsingFailed
	{
		public MissingExpression(Body body) : base(body) { }
	}

	public sealed class MissingInnerBody : ParsingFailed
	{
		public MissingInnerBody(Body body) : base(body) { }
	}

	public sealed class UsageOfInferredVariable : ParsingFailed
	{
		public UsageOfInferredVariable(Body body) : base(body) { }
	}
}