//
// MonoTests.System.Xml.XsdValidatingReaderTests.cs
//
// Author:
//	Atsushi Enomoto <ginga@kit.hi-ho.ne.jp>
//
// (C)2003 Atsushi Enomoto
// (C)2005-2007 Novell, Inc.
//
using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.Schema;
using NUnit.Framework;

namespace MonoTests.System.Xml
{
	[TestFixture]
	public class XsdValidatingReaderTests : Assertion
	{
		public XsdValidatingReaderTests ()
		{
		}

		XmlReader xtr;
		XmlValidatingReader xvr;

		private XmlValidatingReader PrepareXmlReader (string xml)
		{
			XmlReader reader = new XmlTextReader (xml, XmlNodeType.Document, null);
//			XmlDocument doc = new XmlDocument ();
//			doc.LoadXml (xml);
//			XmlReader reader = new XmlNodeReader (doc);

			return new XmlValidatingReader (reader);
		}

		[Test]
		public void TestEmptySchema ()
		{
			string xml = "<root/>";
			xvr = PrepareXmlReader (xml);
			xvr.ValidationType = ValidationType.Schema;
			xvr.Read ();	// Is is missing schema component.
		}

		[Test]
		public void TestSimpleValidation ()
		{
			string xml = "<root/>";
			xvr = PrepareXmlReader (xml);
			AssertEquals (ValidationType.Auto, xvr.ValidationType);
			XmlSchema schema = new XmlSchema ();
			XmlSchemaElement elem = new XmlSchemaElement ();
			elem.Name = "root";
			schema.Items.Add (elem);
			xvr.Schemas.Add (schema);
			xvr.Read ();	// root
			AssertEquals (ValidationType.Auto, xvr.ValidationType);
			xvr.Read ();	// EOF

			xml = "<hoge/>";
			xvr = PrepareXmlReader (xml);
			xvr.Schemas.Add (schema);
			try {
				xvr.Read ();
				Fail ("element mismatch is incorrectly allowed");
			} catch (XmlSchemaException) {
			}

			xml = "<hoge xmlns='urn:foo' />";
			xvr = PrepareXmlReader (xml);
			xvr.Schemas.Add (schema);
			try {
				xvr.Read ();
				Fail ("Element in different namespace is incorrectly allowed.");
			} catch (XmlSchemaException) {
			}
		}

		[Test]
		public void TestReadTypedValueSimple ()
		{
			string xml = "<root>12</root>";
			XmlSchema schema = new XmlSchema ();
			XmlSchemaElement elem = new XmlSchemaElement ();
			elem.Name = "root";
			elem.SchemaTypeName = new XmlQualifiedName ("integer", XmlSchema.Namespace);
			schema.Items.Add (elem);

			// Lap 1:
			
			xvr = PrepareXmlReader (xml);
			xvr.Schemas.Add (schema);
			// Read directly from root.
			object o = xvr.ReadTypedValue ();
			AssertEquals (ReadState.Initial, xvr.ReadState);
			AssertNull (o);

			xvr.Read ();	// element root
			AssertEquals (XmlNodeType.Element, xvr.NodeType);
			AssertNotNull (xvr.SchemaType);
			Assert (xvr.SchemaType is XmlSchemaDatatype);
			o = xvr.ReadTypedValue ();	// read "12"
			AssertEquals (XmlNodeType.EndElement, xvr.NodeType);
			AssertNotNull (o);
			AssertEquals (typeof (decimal), o.GetType ());
			decimal n = (decimal) o;
			AssertEquals (12, n);
			Assert (!xvr.EOF);
			AssertEquals ("root", xvr.Name);
			AssertNull (xvr.SchemaType);	// EndElement's type

			// Lap 2:

			xvr = PrepareXmlReader (xml);
			xvr.Schemas.Add (schema);
			xvr.Read ();	// root
			XmlSchemaDatatype dt = xvr.SchemaType as XmlSchemaDatatype;
			AssertNotNull (dt);
			AssertEquals (typeof (decimal), dt.ValueType);
			AssertEquals (XmlTokenizedType.None, dt.TokenizedType);
			xvr.Read ();	// text "12"
			AssertNull (xvr.SchemaType);
			o = xvr.ReadTypedValue ();
			// ReadTypedValue is different from ReadString().
			AssertNull (o);
		}

		[Test]
		[Ignore ("XML Schema validator should not be available for validating non namespace-aware XmlReader that handled colon as a name character")]
		public void TestNamespacesFalse ()
		{
			// This tests if Namespaces=false is specified, then
			// the reader's NamespaceURI should be always string.Empty and
			// validation should be done against such schema that has target ns as "".
			string xml = "<x:root xmlns:x='urn:foo' />";
			xvr = PrepareXmlReader (xml);
			xvr.Namespaces = false;
			AssertEquals (ValidationType.Auto, xvr.ValidationType);
			XmlSchema schema = new XmlSchema ();
			schema.TargetNamespace = "urn:foo";
			XmlSchemaElement elem = new XmlSchemaElement ();
			elem.Name = "root";
			schema.Items.Add (elem);
			xvr.Schemas.Add (schema);
			xvr.Read ();	// root
			Assert (!xvr.Namespaces);
			AssertEquals ("x:root", xvr.Name);
			// LocalName may contain colons.
			AssertEquals ("x:root", xvr.LocalName);
			// NamespaceURI is not supplied.
			AssertEquals ("", xvr.NamespaceURI);
		}

		[Test]
		public void TestReadTypedAttributeValue ()
		{
			string xml = "<root attr='12'></root>";
			XmlSchema schema = new XmlSchema ();
			XmlSchemaElement elem = new XmlSchemaElement ();
			elem.Name = "root";
			XmlSchemaComplexType ct = new XmlSchemaComplexType ();
			XmlSchemaAttribute attr = new XmlSchemaAttribute ();
			attr.Name = "attr";
			attr.SchemaTypeName = new XmlQualifiedName ("int", XmlSchema.Namespace);
			ct.Attributes.Add (attr);
			elem.SchemaType = ct;
			schema.Items.Add (elem);

			xvr = PrepareXmlReader (xml);
			xvr.Schemas.Add (schema);
			xvr.Read ();
			AssertEquals ("root", xvr.Name);
			Assert (xvr.MoveToNextAttribute ());	// attr
			AssertEquals ("attr", xvr.Name);
			XmlSchemaDatatype dt = xvr.SchemaType as XmlSchemaDatatype;
			AssertNotNull (dt);
			AssertEquals (typeof (int), dt.ValueType);
			AssertEquals (XmlTokenizedType.None, dt.TokenizedType);
			object o = xvr.ReadTypedValue ();
			AssertEquals (XmlNodeType.Attribute, xvr.NodeType);
			AssertEquals (typeof (int), o.GetType ());
			int n = (int) o;
			AssertEquals (12, n);
			Assert (xvr.ReadAttributeValue ());	// can read = seems not proceed.
		}

		[Test]
		public void DuplicateSchemaAssignment ()
		{
			string xml = @"<data
			xmlns='http://www.test.com/schemas/'
			xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'
			xsi:schemaLocation='http://www.test.com/schemas/ /home/user/schema.xsd' />";
			string xsd = @"<xs:schema
			targetNamespace='http://www.test.com/schemas/'
			xmlns:xs='http://www.w3.org/2001/XMLSchema'
			xmlns='http://www.test.com/schemas/' >
		        <xs:element name='data' /></xs:schema>";

			string xmlns = "http://www.test.com/schemas/";

			XmlValidatingReader xvr = new XmlValidatingReader (
				xml, XmlNodeType.Document, null);
			XmlSchemaCollection schemas = new XmlSchemaCollection ();
			schemas.Add (XmlSchema.Read (new XmlTextReader (
				xsd, XmlNodeType.Document, null), null));
			xvr.Schemas.Add (schemas);
			while (!xvr.EOF)
				xvr.Read ();
		}

		[Test] // bug #76234
		public void DTDValidatorNamespaceHandling ()
		{
			string xml = "<xml xmlns='urn:a'> <foo> <a:bar xmlns='urn:b' xmlns:a='urn:a' /> <bug /> </foo> </xml>";
			XmlValidatingReader vr = new XmlValidatingReader (
				xml, XmlNodeType.Document, null);
			vr.Read ();
			vr.Read (); // whitespace
			AssertEquals ("#1", String.Empty, vr.NamespaceURI);
			vr.Read (); // foo
			AssertEquals ("#2", "urn:a", vr.NamespaceURI);
			vr.Read (); // whitespace
			vr.Read (); // a:bar
			AssertEquals ("#3", "urn:a", vr.NamespaceURI);
			vr.Read (); // whitespace
			vr.Read (); // bug
			AssertEquals ("#4", "urn:a", vr.NamespaceURI);
		}

		[Test]
		public void MultipleSchemaInSchemaLocation ()
		{
			XmlTextReader xtr = new XmlTextReader ("Test/XmlFiles/xsd/multi-schemaLocation.xml");
			XmlValidatingReader vr = new XmlValidatingReader (xtr);
			while (!vr.EOF)
				vr.Read ();
		}

		[Test]
		public void ReadTypedValueSimpleTypeRestriction ()
		{
			string xml = "<root>xx</root><!-- after -->";
			string xsd = @"
<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>
  <xs:element name='root'>
    <xs:simpleType>
      <xs:restriction base='xs:string'>
        <xs:minLength value='2' />
      </xs:restriction>
    </xs:simpleType>
  </xs:element>
</xs:schema>";
			XmlTextReader xir = 
				new XmlTextReader (xml, XmlNodeType.Document, null);
			XmlTextReader xsr =
				new XmlTextReader (xsd, XmlNodeType.Document, null);
			XmlValidatingReader vr = new XmlValidatingReader (xir);
			vr.Schemas.Add (XmlSchema.Read (xsr, null));
			vr.Read (); // root
			AssertEquals ("xx", vr.ReadTypedValue ());
			AssertEquals (XmlNodeType.EndElement, vr.NodeType);
		}

		// If we normalize string before validating with facets,
		// this test will fail. It will also fail if ReadTypedValue()
		// ignores whitespace nodes.
		[Test]
		public void ReadTypedValueWhitespaces ()
		{
			string xml = "<root>  </root><!-- after -->";
			string xsd = @"
<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>
  <xs:element name='root'>
    <xs:simpleType>
      <xs:restriction base='xs:string'>
        <xs:minLength value='2' />
      </xs:restriction>
    </xs:simpleType>
  </xs:element>
</xs:schema>";
			XmlTextReader xir = 
				new XmlTextReader (xml, XmlNodeType.Document, null);
			XmlTextReader xsr =
				new XmlTextReader (xsd, XmlNodeType.Document, null);
			XmlValidatingReader vr = new XmlValidatingReader (xir);
			vr.Schemas.Add (XmlSchema.Read (xsr, null));
			vr.Read (); // root
			AssertEquals ("  ", vr.ReadTypedValue ());
			AssertEquals (XmlNodeType.EndElement, vr.NodeType);
		}

		[Test] // bug #77241
		public void EmptyContentAllowWhitespace ()
		{
			string doc = @"
<root>
        <!-- some comment -->
        <child/>
</root>
";
			string schema = @"
<xsd:schema xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
    <xsd:element name=""root"">
        <xsd:complexType>
            <xsd:sequence>
                <xsd:element name=""child"" type=""xsd:string"" />
            </xsd:sequence>
        </xsd:complexType>
    </xsd:element>
</xsd:schema>
";
			XmlValidatingReader reader = new XmlValidatingReader (
				new XmlTextReader (new StringReader (doc)));
			reader.Schemas.Add (null,
				new XmlTextReader (new StringReader (schema)));
			while (reader.Read ())
				;
		}

		[Test] // bug #79650
#if NET_2_0
		// annoyance
		[ExpectedException (typeof (XmlSchemaValidationException))]
#else
		[ExpectedException (typeof (XmlSchemaException))]
#endif
		public void EnumerationFacetOnAttribute ()
		{
			string xml = "<test mode='NOT A ENUMERATION VALUE' />";
			XmlSchema schema = XmlSchema.Read (new XmlTextReader ("Test/XmlFiles/xsd/79650.xsd"), null);
			XmlValidatingReader xvr = new XmlValidatingReader (xml, XmlNodeType.Document, null);
			xvr.ValidationType = ValidationType.Schema;
			xvr.Schemas.Add (schema);
			while (!xvr.EOF)
				xvr.Read ();
		}

		class XmlErrorResolver : XmlResolver
		{
			public override ICredentials Credentials {
				set { }
			}

			public override object GetEntity (Uri uri, string role, Type type)
			{
				throw new Exception ();
			}
		}

		[Test] // bug #79924
		public void ValidationTypeNoneIgnoreSchemaLocations ()
		{
			string xml = "<project xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:noNamespaceSchemaLocation='nosuchschema.xsd'/>";
			XmlValidatingReader vr = new XmlValidatingReader (
				new XmlTextReader (new StringReader (xml)));
			vr.ValidationType = ValidationType.None;
			vr.XmlResolver = new XmlErrorResolver ();
			while (!vr.EOF)
				vr.Read ();
		}

		[Test] // bug #336625
		public void ValidationTypeNoneIgnoreLocatedSchemaErrors ()
		{
			string xml = "<test xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:noNamespaceSchemaLocation='Test/XmlFiles/xsd/336625.xsd'/>";
			XmlValidatingReader vr = new XmlValidatingReader (
				new XmlTextReader (new StringReader (xml)));
			vr.ValidationType = ValidationType.None;
			while (!vr.EOF)
				vr.Read ();
		}

		[Test]
		public void Bug81360 ()
		{
			string schemaFile = "Test/XmlFiles/xsd/81360.xsd";
			XmlTextReader treader = new XmlTextReader (schemaFile);
			XmlSchema sc = XmlSchema.Read (treader, null);
			sc.Compile (null);
			string xml = @"<body xmlns='" + sc.TargetNamespace + "'><div></div></body>";
			XmlTextReader reader = new XmlTextReader (new StringReader (xml));
			XmlValidatingReader validator = new XmlValidatingReader (reader);
			validator.Schemas.Add (sc);
			validator.ValidationType = ValidationType.Schema;
			while (!validator.EOF)
				validator.Read ();
		}

#if NET_2_0
		[Test]
		public void Bug81460 ()
		{
			string xsd = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:element name='foo'><xs:complexType><xs:attribute name='a' default='x' /></xs:complexType></xs:element></xs:schema>";
			string xml = "<foo/>";
			XmlReaderSettings s = new XmlReaderSettings ();
			s.ValidationType = ValidationType.Schema;
			s.Schemas.Add (XmlSchema.Read (new StringReader (xsd), null));
			XmlReader r = XmlReader.Create (new StringReader (xml), s);
			r.Read ();
			r.MoveToFirstAttribute (); // default attribute
			AssertEquals (String.Empty, r.Prefix);
		}
#endif

		[Test]
#if NET_2_0
		// annoyance
		[ExpectedException (typeof (XmlSchemaValidationException))]
#else
		[ExpectedException (typeof (XmlSchemaException))]
#endif
		public void Bug82099 ()
		{
			string xsd = @"
<xsd:schema xmlns:xsd='http://www.w3.org/2001/XMLSchema'>
  <xsd:element name='Customer' type='CustomerType' />
  <xsd:complexType name='CustomerType'>
    <xsd:attribute name='name' type='xsd:string' />
  </xsd:complexType>
</xsd:schema>";
			XmlSchema schema = XmlSchema.Read (new StringReader (xsd), null);

			string xml = "<Customer name='Bob'> </Customer>";

#if NET_2_0
			XmlReaderSettings settings = new XmlReaderSettings ();
			settings.Schemas.Add (schema);
			settings.ValidationType = ValidationType.Schema;

			XmlReader reader = XmlReader.Create (new StringReader (xml), settings);
			
#else
			XmlValidatingReader reader = new XmlValidatingReader (xml, XmlNodeType.Document, null);
			reader.Schemas.Add (schema);
			reader.ValidationType = ValidationType.Schema;
#endif
			reader.Read ();
			reader.Read ();
			reader.Read ();
		}

		[Test]
		public void Bug82010 ()
		{
			string xmlfile = "Test/XmlFiles/xsd/82010.xml";
			string xsdfile = "Test/XmlFiles/xsd/82010.xsd";
			XmlTextReader xr = null, xr2 = null;
			try {
				xr = new XmlTextReader (xsdfile);
				xr2 = new XmlTextReader (xmlfile);
				XmlValidatingReader xvr = new XmlValidatingReader (xr2);
				xvr.Schemas.Add (XmlSchema.Read (xr, null));
				while (!xvr.EOF)
					xvr.Read ();
			} finally {
				if (xr2 != null)
					xr2.Close ();
				if (xr != null)
					xr.Close ();
			}
		}

		[Test]
		public void Bug376395 ()
		{
			string xmlfile = "Test/XmlFiles/xsd/376395.xml";
			string xsdfile = "Test/XmlFiles/xsd/376395.xsd";
			XmlTextReader xr = null, xr2 = null;
			try {
				xr = new XmlTextReader (xsdfile);
				xr2 = new XmlTextReader (xmlfile);
				XmlValidatingReader xvr = new XmlValidatingReader (xr2);
				xvr.Schemas.Add (XmlSchema.Read (xr, null));
				while (!xvr.EOF)
					xvr.Read ();
			} finally {
				if (xr2 != null)
					xr2.Close ();
				if (xr != null)
					xr.Close ();
			}
		}

		[Test]
		public void ValidateMixedInsideXsdAny ()
		{
			string xml = @"<root xmlns='urn:foo'>
  <X><Z>text</Z></X>
  <Y><X><Z>text</Z></X></Y>
</root>";
			string xsd = @"
<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'
  targetNamespace='urn:foo' xmlns='urn:foo'>
    <xs:complexType name='root-type'>
      <xs:sequence><xs:element ref='X' /><xs:element ref='Y' /></xs:sequence>
    </xs:complexType>
    <xs:complexType name='X-type'>
      <xs:choice minOccurs='1' maxOccurs='unbounded'>
        <xs:any processContents='skip'/>
      </xs:choice>
    </xs:complexType>
    <xs:complexType name='Y-type'>
      <xs:sequence><xs:element ref='X' /></xs:sequence>
    </xs:complexType>
  <xs:element name='root' type='root-type' />
  <xs:element name='X' type='X-type' />
  <xs:element name='Y' type='Y-type' />
</xs:schema>";
			XmlTextReader xtr = new XmlTextReader (new StringReader (xml));
			XmlValidatingReader xvr = new XmlValidatingReader (xtr);
			XmlReader xsr = new XmlTextReader (new StringReader (xsd));
			xvr.Schemas.Add (XmlSchema.Read (xsr, null));
			while (!xvr.EOF)
				xvr.Read ();
#if NET_2_0
			xtr = new XmlTextReader (new StringReader (xml));
			xsr = new XmlTextReader (new StringReader (xsd));
			var s = new XmlReaderSettings ();
			s.Schemas.Add (XmlSchema.Read (xsr, null));
			s.ValidationType = ValidationType.Schema;
			XmlReader xvr2 = XmlReader.Create (xtr, s);
			while (!xvr2.EOF)
				xvr2.Read ();
#endif
		}

#if NET_2_0
		[Test]
		public void WhitespaceAndElementOnly ()
		{
			string xsd = @"<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>
  <xs:element name='element_list'>
    <xs:complexType>
      <xs:sequence>
        <xs:element name='element' maxOccurs='unbounded' />
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>";
			string xml = @"<element_list>
    <!-- blah blah blah -->
    <element />

    <!-- blah blah -->
    <element />
</element_list>";
			RunValidation (xml, xsd);
		}

		[Test]
		[ExpectedException (typeof (XmlSchemaValidationException))]
		public void EnumerationFacet ()
		{
			// bug #339934
			string xsd = @"<xs:schema id='schema' xmlns:xs='http://www.w3.org/2001/XMLSchema'>
    <xs:simpleType name='ModeType'>
        <xs:restriction base='xs:string'>
            <xs:enumeration value='on' />
            <xs:enumeration value='off' />
        </xs:restriction>
    </xs:simpleType>
    <xs:element name='test'>
        <xs:complexType>
            <xs:sequence/>
            <xs:attribute name='mode' type='ModeType' use='required' />
        </xs:complexType>
    </xs:element>
</xs:schema>";
			string xml = @"<test mode='out of scope'></test>";

			RunValidation (xml, xsd);
		}

		void RunValidation (string xml, string xsd)
		{
			XmlReaderSettings s = new XmlReaderSettings ();
			s.ValidationType = ValidationType.Schema;
			s.Schemas.Add (XmlSchema.Read (XmlReader.Create (new StringReader (xsd)), null));

			XmlReader r = XmlReader.Create (new StringReader (xml), s);
			while (!r.EOF)
				r.Read ();
		}
#endif
	}
}
