function init() {
    $(document).keypress(function (Event) { kCode(Event); });
    $("#savefile").click(function (Event) { doFileUpload(); });
    $("#deletefile").click(function (Event) { deleteFile(); });

    setInterval(updateStats, 2500);
}

var konami = [112, 114, 105, 115, 99, 105, 108, 108, 97];
var kStep = 0;
var kFull = 9;

function kCode(Event) {
    
    var code = Event.keyCode || Event.which;
    
    if (kStep < konami.length && code == konami[kStep]) {
        kStep++;

        if (kStep == konami.length) {
            $(".pulldown").animate({top: "-2px"}, 1000);
        }
    } else if (kStep < konami.length) {
        kStep = 0;
    }
}

function FormatNum(Number, Suffix) {
    Number += '';
    var x = nStr.split('.');
    var x1 = x[0];
    var x2 = x.length > 1 ? '.' + x[1] : '';
    var rgx = /(\d+)(\d{3})/;
    while (rgx.test(x1)) {
        x1 = x1.replace(rgx, '$1' + ',' + '$2');
    }
    return x1 + x2 + Suffix;
}

function updateStats() {
    // Download stats from /Data

    $.getJSON("Data", function (Data) {
        $("OvenTemp").text(FormatNum(Data.OvenTemperature, " °C") + " (" + FormatNum(Data.TSense1, " °C") + ", " + FormatNum(Data.TSense2, " °C"));
        $("BayTemp").text(FormatNum(Data.BayTemperature), " °C");
        $("FreeMem").text(FormatNum(Data.FreeMem, " B"));
        $("DoorAjar").text(Data.DoorAjar ? "Ajar" : "Closed");
        $("ElementStatuses").text(FormatNum(Data.LowerPower, "%") + ", " + FormatNum(Data.UpperPower, "%"));
        $("CPULoad").text(FormatNum(Data.Load, "%"));
        $("OvenFan").text(FormatNum(Data.Fan2, "%"));
    });

}

function deleteFile() {
    var fileName = $("#path").val();
    var Request = new XMLHttpRequest();
    Request.open("GET", "File/Delete", false);
    Request.send("f=" + fileName);
}

function doFileUpload() {
    var file = $("#file")[0].files[0];

    var reader = new FileReader();
    reader.onload = function (evt) {
        var fileData = evt.target.result;
        var fileName = $("#path").val();
        var bytes = new Uint8Array(fileData);
        var binaryText = '';
        var fullText = '';

        var chunksize = 0;

        for (var index = 0; index < bytes.byteLength; index++) {
            binaryText += String.fromCharCode(bytes[index]);
            chunksize++;

            if (chunksize == 256 || index >= bytes.byteLength - 1) {
                chunksize = 0;
                fullText += binaryText;
                binaryText = btoa(binaryText);

                var Request = new XMLHttpRequest();
                Request.open("POST", "File/Write", false);
                Request.send("f=" + fileName + "&d=" + btoa(binaryText));

                binaryText = "";
            }
        }

        console.log("Full Base64 is: ");
        console.log(btoa(fullText));

    };
    reader.readAsArrayBuffer(file);
}