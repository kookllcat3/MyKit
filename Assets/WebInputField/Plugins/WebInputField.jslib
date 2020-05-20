mergeInto(LibraryManager.library, {

    InputBox: function (str, callbackObj, callbackFunc) {
        var msg = prompt("Please enter message", Pointer_stringify(str));
        if (msg != null && callbackObj != null && callbackFunc != null) {
            SendMessage(Pointer_stringify(callbackObj), Pointer_stringify(callbackFunc), msg);
        }
    },


});

