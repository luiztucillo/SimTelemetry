﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SimTelemetry.Domain.Common;

namespace SimTelemetry.Tests.Core
{
    [TestFixture]
    public class InMemoryRepositoryTests
    {
        [Test]
        public void InMemoryTests()
        {
            var obj1 = new InMemoryObject("1", "Test 1");
            var obj2 = new InMemoryObject("A", "Test 2");
            var obj3 = new InMemoryObject("!", "Test 3");
            var rep = new InMemoryRepository<InMemoryObject>();

            Assert.AreEqual(rep.GetAll().Count(x => true), 0);
            Assert.AreEqual(rep.Contains(obj1), false);
            Assert.AreEqual(rep.Contains(obj2), false);
            Assert.AreEqual(rep.Contains(obj3), false);

            // Add 1
            rep.Add(obj1);

            Assert.AreEqual(rep.GetAll().Count(x => true), 1);
            Assert.AreEqual(rep.Contains(obj1), true);
            Assert.AreEqual(rep.Contains(obj2), false);
            Assert.AreEqual(rep.Contains(obj3), false);

            // Add 2
            rep.Add(obj2);

            Assert.AreEqual(rep.GetAll().Count(x => true), 2);
            Assert.AreEqual(rep.Contains(obj1), true);
            Assert.AreEqual(rep.Contains(obj2), true);
            Assert.AreEqual(rep.Contains(obj3), false);

            // Add 3
            rep.Add(obj3);

            Assert.AreEqual(rep.GetAll().Count(x => true), 3);
            Assert.AreEqual(rep.Contains(obj1), true);
            Assert.AreEqual(rep.Contains(obj2), true);
            Assert.AreEqual(rep.Contains(obj3), true);

            // Readd
            rep.Add(obj3);

            Assert.AreEqual(rep.GetAll().Count(x => true), 3);
            Assert.AreEqual(rep.Contains(obj1), true);
            Assert.AreEqual(rep.Contains(obj2), true);
            Assert.AreEqual(rep.Contains(obj3), true);

            // Remove 2
            rep.Remove(obj2);

            Assert.AreEqual(rep.GetAll().Count(x => true), 2);
            Assert.AreEqual(rep.Contains(obj1), true);
            Assert.AreEqual(rep.Contains(obj2), false);
            Assert.AreEqual(rep.Contains(obj3), true);

            // Remove 1
            rep.Remove(obj1);

            Assert.AreEqual(rep.GetAll().Count(x => true), 1);
            Assert.AreEqual(rep.Contains(obj1), false);
            Assert.AreEqual(rep.Contains(obj2), false);
            Assert.AreEqual(rep.Contains(obj3), true);

            // Add 1 & 2
            rep.AddRange(new[] { obj1, obj2 });

            Assert.AreEqual(rep.GetAll().Count(x => true), 3);
            Assert.AreEqual(rep.Contains(obj1), true);
            Assert.AreEqual(rep.Contains(obj2), true);
            Assert.AreEqual(rep.Contains(obj3), true);

            rep.Clear();

            Assert.AreEqual(rep.GetAll().Count(x => true), 0);
            Assert.AreEqual(rep.Contains(obj1), false);
            Assert.AreEqual(rep.Contains(obj2), false);
            Assert.AreEqual(rep.Contains(obj3), false);

        }

        [Test]
        public void LazyInMemoryReadOnlyTest()
        {
            var myObjectCreator = new InMemoryObjectDataSource();
            var lazyRepo = new LazyInMemoryRepository<InMemoryObject, string>(myObjectCreator);

            // Unitialized
            Assert.AreEqual(0, myObjectCreator.GetIdsCalls);
            Assert.AreEqual(0, myObjectCreator.AddObjectCalls);
            Assert.AreEqual(0, myObjectCreator.GetObjectCalls);
            Assert.AreEqual(0, myObjectCreator.RemoveObjectCalls);

            // Only peek what's out there:
            Assert.AreEqual(false, lazyRepo.Contains("A"));
            Assert.AreEqual(true, lazyRepo.Contains("B"));
            Assert.AreEqual(false, lazyRepo.Contains("C"));
            Assert.AreEqual(true, lazyRepo.Contains("1"));
            Assert.AreEqual(false, lazyRepo.Contains("2"));
            Assert.AreEqual(true, lazyRepo.Contains("!"));
            Assert.AreEqual(false, lazyRepo.Contains("@"));

            Assert.AreEqual(1, myObjectCreator.GetIdsCalls);
            Assert.AreEqual(0, myObjectCreator.AddObjectCalls);
            Assert.AreEqual(0, myObjectCreator.GetObjectCalls);
            Assert.AreEqual(0, myObjectCreator.RemoveObjectCalls);

            // Now let's get an object:
            var myObjectB = lazyRepo.GetById("B");
            Assert.AreEqual("B", myObjectB.ID);
            Assert.AreEqual("Test 2", myObjectB.Test);

            Assert.AreEqual(1, myObjectCreator.GetIdsCalls);
            Assert.AreEqual(0, myObjectCreator.AddObjectCalls);
            Assert.AreEqual(1, myObjectCreator.GetObjectCalls);
            Assert.AreEqual(0, myObjectCreator.RemoveObjectCalls);

            // Try to add an object.
            var myObjectA = new InMemoryObject("A", "Test A");
            Assert.AreEqual(false, lazyRepo.Add(myObjectA)); // read-only!
            Assert.AreEqual(1, myObjectCreator.GetIdsCalls);
            Assert.AreEqual(1, myObjectCreator.AddObjectCalls);
            Assert.AreEqual(1, myObjectCreator.GetObjectCalls);
            Assert.AreEqual(0, myObjectCreator.RemoveObjectCalls);

            // Try to remove an object.
            Assert.AreEqual(false, lazyRepo.Remove(myObjectB)); // read-only!
            Assert.AreEqual(1, myObjectCreator.GetIdsCalls);
            Assert.AreEqual(1, myObjectCreator.AddObjectCalls);
            Assert.AreEqual(1, myObjectCreator.GetObjectCalls);
            Assert.AreEqual(1, myObjectCreator.RemoveObjectCalls);

            // Try to remove an object (that doens't even exist).
            Assert.AreEqual(false, lazyRepo.Remove(myObjectA)); // read-only!
            Assert.AreEqual(1, myObjectCreator.GetIdsCalls);
            Assert.AreEqual(1, myObjectCreator.AddObjectCalls);
            Assert.AreEqual(1, myObjectCreator.GetObjectCalls);
            Assert.AreEqual(2, myObjectCreator.RemoveObjectCalls);

            // After trying to remove it, try to get object B again:
            var myObjectB_2 = lazyRepo.GetById("B");
            Assert.AreEqual(true, myObjectB_2.Equals(myObjectB));
            Assert.AreEqual(1, myObjectCreator.GetIdsCalls);
            Assert.AreEqual(1, myObjectCreator.AddObjectCalls);
            Assert.AreEqual(1, myObjectCreator.GetObjectCalls); // we already had this object in data list.
            Assert.AreEqual(2, myObjectCreator.RemoveObjectCalls);
            Assert.AreEqual(1, lazyRepo.GetAll().ToList().Count );

            // After trying to remove it, try to get object  again:
            var myObjectC = lazyRepo.GetById("C");
            Assert.AreEqual(null, myObjectC.ID);
            Assert.AreEqual(1, myObjectCreator.GetIdsCalls);
            Assert.AreEqual(1, myObjectCreator.AddObjectCalls);
            Assert.AreEqual(2, myObjectCreator.GetObjectCalls); // tried to get C
            Assert.AreEqual(2, myObjectCreator.RemoveObjectCalls);
            Assert.AreEqual(1, lazyRepo.GetAll().ToList().Count);

            // Have object '!' ready, and try to see if it contains it.
            var objectExclamationMark = new InMemoryObject("!", "Test 3");
            Assert.AreEqual(false, lazyRepo.Contains(objectExclamationMark)); // we haven't pulled the object out of the repo!
            Assert.AreEqual(1, myObjectCreator.GetIdsCalls);
            Assert.AreEqual(1, myObjectCreator.AddObjectCalls);
            Assert.AreEqual(2, myObjectCreator.GetObjectCalls);
            Assert.AreEqual(2, myObjectCreator.RemoveObjectCalls);
            Assert.AreEqual(1, lazyRepo.GetAll().ToList().Count);
        }
    }

    public class InMemoryObjectDataSource : ILazyRepositoryDataSource<InMemoryObject, string>
    {
        // Specific to testing:
        public int GetIdsCalls { get; private set; }
        public int AddObjectCalls { get; private set; }
        public int GetObjectCalls { get; private set; }
        public int RemoveObjectCalls { get; private set; }

        public InMemoryObjectDataSource()
        {
            GetIdsCalls = 0;
            AddObjectCalls = 0;
            GetObjectCalls = 0;
            RemoveObjectCalls = 0;

        }

        public IList<string> GetIds()
        {
            GetIdsCalls++;

            // Do I/O, DB, stuff here
            var ids = new List<string> {"1", "B", "!"};
            return ids;
        }

        public bool Add(InMemoryObject obj)
        {
            AddObjectCalls++;

            // It's get-only repo.
            return false;
        }

        public InMemoryObject Get(string id)
        {
            GetObjectCalls++;

            switch (id)
            {
                case "1":
                    return new InMemoryObject("1", "Test 1");
                    break;
                case "B":
                    return new InMemoryObject("B", "Test 2");
                    break;
                case "!":
                    return new InMemoryObject("!", "Test 3");
                    break;
            }
            return new InMemoryObject();
        }

        public bool Remove(string Id)
        {
            RemoveObjectCalls++;

            // It's get only repo (in this test)
            // It will however, remove the ID.
            return false;
        }

        public bool Clear()
        {
            return false; // read-only
        }
    }

    public class InMemoryObject : IEntity<string>, IEquatable<InMemoryObject>
    {
        public string ID { get; private set; }
        public string Test { get; private set; }

        public InMemoryObject()
        {
        }

        public InMemoryObject(string id, string test)
        {
            ID = id;
            Test = test;
        }

        public bool Equals(InMemoryObject other)
        {
            return ID.Equals(other.ID) && Test.Equals(other.Test);
        }
    }
}