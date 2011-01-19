function test(oType)
{
    // no parentheses around Array
    var arr = new Array(Array,Function,Boolean);
    // no prentheses around arr[0]
    var foo = new arr[0](1, 2, 3);
    
    // KEEP parentheses around Type.getType(oType.sObjectType)
    return new (Type.getType(oType.sObjectType))(
        oType.sTypeName,
        oType.iVersion,
        oType.sRootElementType,
        oType.sRootElementName);
}

function foo()
{
    return new Date(new Date().ToUTCString()); 
}
