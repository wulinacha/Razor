// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.LanguageServices.Razor.RQNames
{
    internal static class RQNameParser
    {
        public static RQNameNode Parse(string input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var parser = new Parser(input);
            return parser.Parse();
        }

        private class Parser
        {
            private readonly string _input;
            private int _index;

            public Parser(string input)
            {
                _input = input;
            }

            public RQNameNode Parse()
            {
                // rq_name:= rq_ns | rq_agg | rq_membvar | rq_event | rq_meth | rq_prop
                var next = Peek();
                if (next.Kind != TokenKind.Text)
                {
                    Accept(TokenKind.Text);
                }

                switch (next.Text)
                {
                    // rq_ns := "Ns" "(" rq_sym_name_list ")"
                    case "Ns":
                        return ParseNamespace();

                    // rq_agg := "Agg" "(" rq_sym_name_list ")"
                    case "Agg":
                        return ParseAggregate();

                    // rq_membvar:= "Membvar" "(" rq_agg "," rq_sym_name ")"
                    case "Membvar":
                        return ParseMemberVariable();

                    // rq_event:= "Event" "(" rq_agg "," rq_sym_name ")"
                    case "Event":
                        return ParseEvent();

                    // rq_meth:= "Meth" "(" rq_agg "," rq_sym_name "," rq_typevarcount "," rq_params ")"
                    case "Meth":
                        return ParseMethod();

                    // rq_prop := "Prop" "(" rq_agg "," rq_sym_name "," rq_typevarcount "," rq_params ")"
                    case "Prop":
                        return ParseProperty();

                    default:
                        throw new ArgumentException("Invalid RQName: " + _input);
                }
            }

            private RQNameNode ParseNamespace()
            {
                // rq_ns := "Ns" "(" rq_sym_name_list ")"
                Accept(TokenKind.Text, "Ns");
                Accept(TokenKind.LParen);

                var names = ParseSymbolNameList();
                Accept(TokenKind.RParen);

                return new NamespaceNode(names);
            }

            private AggregateNode ParseAggregate()
            {
                // rq_agg := "Agg" "(" rq_sym_name_list ")"
                Accept(TokenKind.Text, "Agg");
                Accept(TokenKind.LParen);

                var names = ParseSymbolNameList();
                Accept(TokenKind.RParen);

                return new AggregateNode(names);
            }

            private MemberVariableNode ParseMemberVariable()
            {
                // rq_membvar:= "Membvar" "(" rq_agg "," rq_sym_name ")"
                Accept(TokenKind.Text, "Membvar");
                Accept(TokenKind.LParen);

                var aggregate = ParseAggregate();
                Accept(TokenKind.Comma);

                var symbolName = ParseSymbolName();
                Accept(TokenKind.RParen);

                return new MemberVariableNode(aggregate, symbolName);
            }

            private EventNode ParseEvent()
            {
                // rq_event := "Event" "(" rq_agg "," rq_sym_name ")"
                Accept(TokenKind.Text, "Event");
                Accept(TokenKind.LParen);

                var aggregate = ParseAggregate();
                Accept(TokenKind.Comma);

                var symbolName = ParseSymbolName();
                Accept(TokenKind.RParen);

                return new EventNode(aggregate, symbolName);
            }

            private MethodNode ParseMethod()
            {
                // rq_meth := "Meth" "(" rq_agg "," rq_sym_name "," rq_typevarcount "," rq_params ")"
                Accept(TokenKind.Text, "Meth");
                Accept(TokenKind.LParen);

                var aggregate = ParseAggregate();
                Accept(TokenKind.Comma);

                var symbolName = ParseSymbolName();
                Accept(TokenKind.Comma);

                var typeVariableCount = ParseTypeVariableCount();
                Accept(TokenKind.Comma);

                var parameters = ParseParameters();
                Accept(TokenKind.RParen);

                return new MethodNode(aggregate, symbolName, typeVariableCount, parameters);
            }

            private PropertyNode ParseProperty()
            {
                // rq_prop := "Prop" "(" rq_agg "," rq_sym_name "," rq_typevarcount "," rq_params ")"
                Accept(TokenKind.Text, "Prop");
                Accept(TokenKind.LParen);

                var aggregate = ParseAggregate();
                Accept(TokenKind.Comma);

                var symbolName = ParseSymbolName();
                Accept(TokenKind.Comma);

                var typeVariableCount = ParseTypeVariableCount();
                Accept(TokenKind.Comma);

                var parameters = ParseParameters();
                Accept(TokenKind.RParen);

                return new PropertyNode(aggregate, symbolName, typeVariableCount, parameters);
            }

            private ParametersNode ParseParameters()
            {
                // rq_params := "Params" "(" rq_param_list ")"
                Accept(TokenKind.Text, "Params");
                Accept(TokenKind.LParen);

                var parameterList = ParseParameterList();
                Accept(TokenKind.RParen);

                return new ParametersNode(parameterList);
            }

            private List<ParameterNode> ParseParameterList()
            {
                // rq_param_list := rq_param | rq_param "," rq_param_list
                if (!Optional(TokenKind.Text))
                {
                    // We currently only parse empty parameter lists.
                    return new List<ParameterNode>();
                }

                throw new NotImplementedException();
            }

            private ParameterNode ParseParameter()
            {
                // rq_param:= "Param" "(" rq_type_sig ")"
                throw new NotImplementedException();
            }

            private object ParseTypeSignature()
            {
                // rq_type_sig := rq_aggtype_sig | rq_array_sig | rq_pointer_sig | rq_param_mod_sig | rq_typevar_sig | rq_void_sig | rq_error_sig | rq_null_sig
                throw new NotImplementedException();
            }

            private object ParseAggregateTypeSignature()
            {
                // rq_aggtype_sig := "AggType" "(" rq_agg "," rq_typeparams ")"
                throw new NotImplementedException();
            }

            private object ParseTypeParameters()
            {
                // rq_typeparams := "TypeParams" "(" rq_type_sig_list ")"
                throw new NotImplementedException();
            }

            private object ParseTypeSignatureList()
            {
                // rq_type_sig_list := rq_type_sig | rq_type_sig "," rq_type_sig_list
                throw new NotImplementedException();
            }

            private object ParseArraySignature()
            {
                // rq_array_sig := "Array" "(" rq_rank "," rq_type_sig ")"
                throw new NotImplementedException();
            }

            private object ParsePointerSignature()
            {
                // rq_pointer_sig := "Pointer" "(" rq_type_sig ")"
                throw new NotImplementedException();
            }

            private object ParseParameterModifierSignature()
            {
                // rq_param_mod_sig := "Ref" "(" rq_type_sig ")" | "Out" "(" rq_type_sig ")"
                throw new NotImplementedException();
            }

            private object ParseTypeVariableSignature()
            {
                // rq_typevar_sig := "TyVar" "(" rq_simple_name ")"
                throw new NotImplementedException();
            }

            private object ParseVoidSignature()
            {
                // rq_void_sig := "Void"
                throw new NotImplementedException();
            }

            private object ParseErrorSignature()
            {
                // rq_error_sig := "Error" "(" rq_text ")"
                throw new NotImplementedException();
            }

            private object ParseNullSignature()
            {
                // rq_null_sig := "Null"
                throw new NotImplementedException();
            }

            private List<SymbolNameNode> ParseSymbolNameList()
            {
                // rq_sym_name_list := rq_sym_name | rq_sym_name "," rq_sym_name_list
                var names = new List<SymbolNameNode>();

                names.Add(ParseSymbolName());
                if (Optional(TokenKind.Comma))
                {
                    names.AddRange(ParseSymbolNameList());
                }

                return names;
            }

            private SymbolNameNode ParseSymbolName()
            {
                // rq_sym_name:= rq_aggname | rq_nsname | rq_membvarname | rq_methpropname | rq_intfexplname
                var next = Peek();
                if (next.Kind != TokenKind.Text)
                {
                    Accept(TokenKind.Text);
                }

                switch (next.Text)
                {
                    case "AggName":
                        return ParseAggregateName();

                    case "NsName":
                        return ParseNamespaceName();

                    case "MembvarName":
                        return ParseMemberVariableName();

                    case "IntfExplName":
                        return ParseExplicitInterfaceName();

                    // as rq_methpropname
                    case "MethName":
                        return ParseMethodName();

                    // as rq_methpropname
                    case "PropName":
                        return ParsePropertyName();

                    // as rq_methpropname
                    case "EventName":
                        return ParseEventName();

                    default:
                        throw new ArgumentException("Invalid RQName: " + _input);
                }
            }

            private NamespaceNameNode ParseNamespaceName()
            {
                // rq_nsname := "NsName" "(" rq_simple_name ")"
                Accept(TokenKind.Text, "NsName");
                Accept(TokenKind.LParen);

                var simpleName = ParseSimpleName();
                Accept(TokenKind.RParen);
                return new NamespaceNameNode(simpleName);
            }

            private AggregateNameNode ParseAggregateName()
            {
                // rq_aggname := "AggName" "(" rq_simple_name "," rq_typevarcount ")"
                Accept(TokenKind.Text, "AggName");
                Accept(TokenKind.LParen);

                var simpleName = ParseSimpleName();
                Accept(TokenKind.Comma);

                var typeVariableCount = ParseTypeVariableCount();
                Accept(TokenKind.RParen);

                return new AggregateNameNode(simpleName, typeVariableCount);
            }

            private MemberVariableNameNode ParseMemberVariableName()
            {
                // rq_membvarname := "MembvarName" "(" rq_simple_name ")"
                throw new NotImplementedException();
            }

            private MemberNameNode ParseMemberName()
            {
                // rq_methpropname := rq_methname | rq_propname | rq_eventname
                throw new NotImplementedException();
            }

            private MethodNameNode ParseMethodName()
            {
                // rq_methname := "MethName" "(" rq_simple_name ")"
                throw new NotImplementedException();
            }

            private PropertyNameNode ParsePropertyName()
            {
                // rq_propname := "PropName" "(" rq_simple_name ")"
                Accept(TokenKind.Text, "PropName");
                Accept(TokenKind.LParen);

                var simpleName = ParseSimpleName();
                Accept(TokenKind.RParen);

                return new PropertyNameNode(simpleName);
            }

            private EventNameNode ParseEventName()
            {
                // rq_eventname:= "EventName" "(" rq_simple_name ")"
                throw new NotImplementedException();
            }

            private ExplicitInteraceMemberNameNode ParseExplicitInterfaceName()
            {
                // rq_intfexplname := "IntfExplName" "(" rq_type_sig "," rq_methpropname ")"
                throw new NotImplementedException();
            }

            private TypeVariableCountNode ParseTypeVariableCount()
            {
                // rq_typevarcount := "TypeVarCnt" "(" rq_number ")" 
                Accept(TokenKind.Text, "TypeVarCnt");
                Accept(TokenKind.LParen);
                var count = Accept(TokenKind.Number);
                Accept(TokenKind.RParen);

                return new TypeVariableCountNode(count.Text);
            }

            private SimpleNameNode ParseSimpleName()
            {
                // rq_simple_name = rq_text
                return new SimpleNameNode(Accept(TokenKind.Text).Text);
            }

            private RankNode ParseRank()
            {
                // rq_rank := rq_number 
                return new RankNode(Accept(TokenKind.Number).Text);
            }

            private bool Optional(TokenKind kind)
            {
                var token = Peek();
                if (token.Kind != kind)
                {
                    return false;
                }

                Take(token);
                return true;
            }

            private Token Accept(TokenKind kind, string text = null)
            {
                var token = Peek();
                if (token.Kind != kind)
                {
                    throw new ArgumentException("Invalid RQName: " + _input);
                }

                if (text != null && !string.Equals(text, token.Text))
                {
                    throw new ArgumentException("Invalid RQName: " + _input);
                }

                Take(token);
                return token;
            }

            private Token Peek()
            {
                var start = _index;
                var i = _index;

                var c = _input.Length > i ? _input[i] : '\0';
                if (c == '\0')
                {
                    return new Token(TokenKind.EOF);
                }
                else if (c == '.')
                {
                    i++;
                    return new Token(TokenKind.Period, ".");
                }
                else if (c == ',')
                {
                    i++;
                    return new Token(TokenKind.Comma, ",");
                }
                else if (c == '(')
                {
                    i++;
                    return new Token(TokenKind.LParen, "(");
                }
                else if (c == ')')
                {
                    i++;
                    return new Token(TokenKind.RParen, ")");
                }
                else if (char.IsDigit(c))
                {
                    while (i < _input.Length && char.IsDigit(_input[i++]))
                    {
                    }

                    return new Token(TokenKind.Number, _input.Substring(start, i - start - 1));
                }
                else if (char.IsLetter(c))
                {
                    while (i < _input.Length && c != '.' && c != ',' && c != '(' && c != ')')
                    {
                        c = _input[i++];
                    }

                    return new Token(TokenKind.Text, _input.Substring(start, i - start - 1));
                }

                return new Token(TokenKind.Invalid);
            }

            private void Take(Token token)
            {
                if (token.Text.Length == 0)
                {
                    throw new InvalidOperationException("Attempted to take 0-length token");
                }

                _index += token.Text.Length;
            }
        }

        // Keywords aren't handled by the tokenizer since they aren't reserved words.
        // All keywords would be classified as text.
        private enum TokenKind
        {
            Text,
            Number,
            Period,
            Comma,
            LParen,
            RParen,
            EOF,
            Invalid,
        }

        private struct Token
        {
            public Token(TokenKind kind)
            {
                Kind = kind;
                Text = "";
            }

            public Token(TokenKind kind, string text)
            {
                Kind = kind;
                Text = text;
            }

            public TokenKind Kind { get; }

            public string Text { get; }
        }
    }
}
