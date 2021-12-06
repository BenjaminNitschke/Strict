﻿using System;
using System.Collections.Generic;

namespace Strict.Language;

public static class StringExtensions
{
	public static string[] SplitLines(this string text) =>
		text.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);

	public static string[] SplitWords(this string text) => text.Split(' ');

	public static string[] SplitWordsAndPunctuation(this string text) =>
		text.Split(new[] { ' ', '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);

	public static string ToWordListString<T>(this IEnumerable<T> list) => string.Join(", ", list);

	public static string ToBracketsString<T>(this IReadOnlyCollection<T> list) =>
		list.Count > 0
			? "(" + ToWordListString(list) + ")"
			: "";

	/// <summary>
	/// Faster version of Regex.IsMatch(text, @"^[A-Za-z]+$");
	/// </summary>
	public static bool IsWord(this string text)
	{
		foreach (var c in text)
			if (c is (< 'A' or > 'Z') and (< 'a' or > 'z'))
				return false;
		return true;
	}

	public static string MakeFirstLetterUppercase(this string name) =>
		name[..1].ToUpperInvariant() + name[1..];
}