using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSUnitTest
{
    /// <summary>
    /// Summary description for Preprocessor
    /// </summary>
    [TestClass]
    public class Preprocessor
    {
        public Preprocessor()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void Defines()
        {
            TestHelper.Instance.RunTest();
        }

        [TestMethod]
        public void Defines_ackbar()
        {
            TestHelper.Instance.RunTest("-define:ackbar");
        }

        [TestMethod]
        public void Defines_ackbarmeow()
        {
            TestHelper.Instance.RunTest("-define:ackbar,meow");
        }

        [TestMethod]
        public void BadIfDef()
        {
            TestHelper.Instance.RunTest();
        }

        [TestMethod]
        public void BadDefines()
        {
            TestHelper.Instance.RunTest();
        }
    }
}
