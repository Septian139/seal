﻿using seal.Enumeration;
using seal.Helper;
using seal.Interface;
using seal.Utils;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace seal.IntfImpl
{
    /// <summary>
    /// Provide serialization mechanism between raw data and object model <br/>
    /// This singleton class cannot be inherited.
    /// </summary>
    public sealed class Serializer : ISerialization
    {
        public static Serializer Instance { get; } = new Serializer();

        private Serializer() { }

        public Func<IModel> CreateCtor<T>() where T : IModel
        {
            ConstructorInfo constructor = typeof(T).GetConstructor(new Type[] { });
            UnaryExpression output = Expression.TypeAs(Expression.New(constructor), typeof(IModel));
            Expression<Func<IModel>> creatorExpression = Expression.Lambda<Func<IModel>>(
               output);


            return creatorExpression.Compile();
        }



        public Func<IModel, object> CreateGetter(PropertyInfo property)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(IModel), "i");
            UnaryExpression cast = Expression.Convert(parameter, property.DeclaringType);

            MemberExpression getterBody = Expression.Property(cast, property);
            UnaryExpression output = Expression.Convert(getterBody, typeof(object));

            Expression<Func<IModel, object>> exp = Expression.Lambda<Func<IModel, object>>(
                output, parameter);

            return exp.Compile();
        }

        public Action<IModel, object> CreateSetter(PropertyInfo property)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(IModel), "i");
            UnaryExpression cast = Expression.Convert(parameter, property.DeclaringType);

            ParameterExpression input = Expression.Parameter(typeof(object), "p");
            UnaryExpression conv = Expression.Convert(input, property.PropertyType);
            MemberExpression prop = Expression.Property(cast, property);

            Action<IModel, object> result = Expression.Lambda<Action<IModel, object>>
              (
                  Expression.Assign(prop, conv), parameter, input
              ).Compile();

            return result;
        }

        /// <summary>
        /// Serialize class to Raw format
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="table"></param>
        /// <returns></returns>
        public IList<object> Serialize<T>(T table) where T : IModel
        {
            ModelFactory factory = ModelFactory.GetInstance();
            TableInfo tInfo = factory[typeof(T).Name];
            return table.Unpack();

        }

        /// <summary>
        /// Deserialize raw format to class <br/>
        /// Please note that you cannot directly deserialize raw data from serialize method
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="raw"></param>
        /// <returns></returns>
        public T Deserialize<T>(IList<object> raw) where T : IModel, new()
        {
            ModelFactory factory = ModelFactory.GetInstance();
            TableInfo tInfo = factory[typeof(T).Name];
            T obj = (T)tInfo.Constructor();
            obj.Pack(raw);
            return obj;
        }

        private string ValueConverter(object value)
        {
            if (value == null)
            {
                return "NULL";
            }

            if (value.GetType().IsSubclassOf(typeof(DateTime)) || value is DateTime)
            {
                return "'" + DateTimeUtility.GetSqlFormatDate((DateTime)value) + "'";
            }

            if ((value.GetType().IsSubclassOf(typeof(long)) || value is long) ||
                (value.GetType().IsSubclassOf(typeof(int)) || value is int) ||
                (value.GetType().IsSubclassOf(typeof(short)) || value is short) ||
                (value.GetType().IsSubclassOf(typeof(double)) || value is double) ||
                (value.GetType().IsSubclassOf(typeof(float)) || value is float))
            {
                return value.ToString();
            }

            if (value.GetType().IsSubclassOf(typeof(string)) || value is string)
            {
                return "'" + value.ToString() + "'";
            }

            if (value.GetType().IsSubclassOf(typeof(bool)) || value is bool)
            {
                if ((bool)value == true)
                {
                    return "1";
                }
                return "0";
            }

            if (value.GetType().IsSubclassOf(typeof(IModel)) || value is IModel)
            {
                return ((IModel)value).UniqueIdentifierValue;
            }

            if (value.GetType().IsSubclassOf(typeof(Enum)))
            {
                return ((int)value).ToString();
            }

            throw new ApiException("Invalid value for invoking to database");
        }

        public IDictionary<string, object> CompileQuery(Operation operation, String table, IList<object> raw, string uniqueIdentifierField)
        {
            bool first = true;
            string query = "";

            IDictionary<string, object> compiledQuery = new Dictionary<string, object>();
            IDictionary<string, object> bindings = new Dictionary<string, object>();

            switch (operation)
            {

                case Operation.Insert:
                    query = "INSERT INTO " + table + " ";

                    string column = "(";
                    string value = ") VALUES (";

                    foreach (KeyValuePair<string, object> keyVal in raw)
                    {
                        if (!first)
                        {
                            column += ", ";
                            value += ", ";
                        }

                        first = false;
                        column += keyVal.Key;
                        value += "@" + keyVal.Key;
                        bindings.Add(keyVal.Key, keyVal.Value);
                    }

                    query += column + value + ")";

                    compiledQuery.Add("query", query);
                    compiledQuery.Add("bindings", bindings);
                    break;

                case Operation.Update:
                    query = "UPDATE " + table + " SET ";

                    foreach (KeyValuePair<string, object> keyVal in raw)
                    {
                        if (!first)
                        {
                            query += ", ";
                        }
                        first = false;
                        query += keyVal.Key + " = " + "@" + keyVal.Key;
                        bindings.Add(keyVal.Key, keyVal.Value);
                    }

                    compiledQuery.Add("query", query);
                    compiledQuery.Add("bindings", bindings);
                    break;

                case Operation.Delete:
                    //query = "DELETE FROM " + table + " WHERE " + uniqueIdentifierField + " = " + raw[uniqueIdentifierField];
                    break;

                case Operation.Select:
                    query = "SELECT ";
                    foreach (KeyValuePair<string, object> keyVal in raw)
                    {
                        if (!first)
                        {
                            query += ", ";
                        }
                        first = false;
                        query += keyVal.Key;
                    }

                    query += "FROM " + table;
                    break;

                default:
                    throw new ApiException("Invalid SQL operation");
            }

            return compiledQuery;
        }
    }
}
