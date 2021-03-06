﻿using seal.Attributes;
using seal.Enumeration;
using System;

namespace seal.Base
{
    public class ModelTable : ModelBase
    {
        public override void LazyInit(object value)
        {
            Id = Convert.ToInt32(value);
        }
        public override JoinMode RelationJoinMode => JoinMode.Once;

        internal void HasInitialized(int id)
        {
            Id = id;
            isInitialized = true;
        }

        //public ModelTable(int value) : base()
        //{
        //    Id = value;
        //}

        [Column("Id")]
        public int Id { get; set; }

        [Column("Created")]
        public DateTime Created { get; set; }

        [Column("LastModified")]
        public DateTime LastModified { get; set; }

        public override string UniqueIdentifier => "Id";

        public override string UniqueIdentifierValue => Id.ToString();
    }
}
