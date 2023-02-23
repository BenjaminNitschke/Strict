﻿using System;

namespace Strict.Language;

public sealed class Parameter : NamedType
{
	public Parameter(Type parentType, string name, Expression defaultValue) : base(parentType,
		IsNameStartsWithMutable(name)
			? name[Type.MutableWithSpaceAtEnd.Length..]
			: name,
		defaultValue.ReturnType)
	{
		DefaultValue = defaultValue;
		IsMutable = name.Contains(Type.MutableWithSpaceAtEnd, StringComparison.Ordinal);
	}

	private static bool IsNameStartsWithMutable(string nameAndType) =>
		nameAndType.StartsWith(Type.MutableWithSpaceAtEnd, StringComparison.Ordinal);

	public Expression? DefaultValue { get; private set; }

	public Parameter(Type parentType, string nameAndType) : base(parentType,
		IsNameStartsWithMutable(nameAndType)
			? nameAndType[Type.MutableWithSpaceAtEnd.Length..]
			: nameAndType) =>
		IsMutable = IsNameStartsWithMutable(nameAndType);

	public Parameter CloneWithImplementationType(Type newType)
	{
		if (Type == newType)
			return this;
		var clone = (Parameter)MemberwiseClone();
		clone.Type = newType;
		return clone;
	}

	public void UpdateValue(Expression newExpression, Body bodyForErrorMessage)
	{
		if (!IsMutable)
			throw new Body.ValueIsNotMutableAndCannotBeChanged(bodyForErrorMessage, Name);
		DefaultValue = newExpression;
	}
}