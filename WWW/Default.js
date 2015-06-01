function init() {
    $(document).keypress(function (Event) { kCode(Event); });
    $("#savefile").click(function (Event) { doFileUpload(); });
    $("#deletefile").click(function (Event) { deleteFile(); });

    //setInterval(updateStats, 2500);
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
    } else {
        kStep = 0;
    }
}

function updateStats() {
    // Download stats from /Data

    var Request = new XMLHttpRequest();

    Request.open("GET", "Data", false);
    Request.send();

    // Update stats pane
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