/*! screenlog - v0.3.0 - 2019-01-25
* https://github.com/chinchang/screenlog.js
* Copyright (c) 2019 Kushagra Gour; Licensed  */

(function() {

  var logEl,
      logRows,
      logTitle,
      isInitialized = false,
      _console = {}; // backup console obj to contain references of overridden fns.
      _options = {
        bgColor: "black",
        logColor: "lightgreen",
        infoColor: "blue",
        warnColor: "orange",
        errorColor: "red",
        fontSize: "1em",
        freeConsole: false,
        css: "",
        autoScroll: true,
        startTop: 0
      };

  function createElement(tag, className, css) {
    var element = document.createElement(tag);
    //element.style.cssText = css;
    element.className = className;
    return element;
  }

  function createPanel() {
    logEl = $('<div class="chinchang-screenLog"></div>')[0];
    logTitle = $('<div class="chinchang-screenLog-title">js log <button class="chinchang-screenLog-btnClear">clear</button> (click line for trace)</div>')[0];
    logEl.appendChild(logTitle);
    logTrace = $('<pre class="chinchang-stackTrace" style="display:none"></pre>')[0];
    logEl.appendChild(logTrace);
    logRows = $('<div class="chinchang-screenLog-rows"></div>')[0];
    logEl.appendChild(logRows);
    logEl.style = "z-index:2147483647;font-family:Helvetica,Arial,sans-serif;font-size:" + _options.fontSize
      + ";position:fixed;right:0;top:" + _options.startTop + "px"
      + ";padding:5px;text-align:left;opacity:0.8;min-width:300px;max-height:50vh;overflow:auto"
      + ";background:" + _options.bgColor
      + ";display:none"
      + ";" + _options.css;
    return;
    /*
    var div = createElement(
      "div",
      "chinchang-screenLog",
      "z-index:2147483647;font-family:Helvetica,Arial,sans-serif;font-size:" +
        _options.fontSize +
        ";position:fixed;right:0;top:" + _options.startTop + "px" +
        ";padding:5px;text-align:left;opacity:0.8;min-width:300px;max-height:50vh;overflow:auto;background:" +
        _options.bgColor +
        ";" +
        _options.css
    );
    div.style.display = "none";
    return div;
    */
  }

  function genericLogger(color, className) {

    return function () {

      var el = createElement(
        "div",
        "chinchang-screenLog-row"
          + ((className) ? (" " + className) : "")
          + (logRows.children.length % 2 ? " chinchang-screenLog-altrow" : ""), // zebra lines
        "line-height:1.7em;min-height:1.7em;color:" + color
      );

      // Join arguments into a single string separated by spaces (same as how console.log works).
      // If an argument is a server "responseLog" object, join the text and trace.
      // Surrounding trace with [[..]] identifies it for later.
      var val = "" + [].slice.call(arguments).reduce(function (prev, arg) {
        var argStr = "";
        if (typeof arg == "object") {
          if (arg.LogText) {
            arg = arg.LogText + " [[" + arg.LogTrace + "]]";
          }
          try {
            argStr = JSON.stringify(arg).replace(/^"|"$/g, ""); // Remove surrounding quotes.
          } catch (ex) {
            argStr = arg;
          }
        } else {
          argStr = arg;
        }
        return prev + " " + argStr;
      }, "");

      // Check if there's a trace string on the end, within "[[...]]"
      // If so, remove the trace string and insert before the js trace.
      var traceStr = "";
      var tracePos1 = val.indexOf("[[");
      if (tracePos1 >= 0) {
        var tracePos2 = val.indexOf("]]");
        if (tracePos2 > tracePos1) {
          traceStr = val.substring(tracePos1 + 2, tracePos2) + "\n";
          val = val.substring(0, tracePos1);
        }
      }
      el.textContent = val;
      el.innerHTML = el.textContent.replace(/\n/g, "<br>");
      var stack = traceStr + SimpleStack();
      $(el).click(function () {
        var $el = $(this);
        var $trace = $(".chinchang-stackTrace");
        if ($el.next().is($trace) && $trace.is(":visible")) {
          $el.next().hide();
        } else {
          $trace.text(stack).insertAfter($el).show();
        }
      });

      logEl.style.display = "block"; // show panel if hidden
      logRows.appendChild(el);

      // Scroll to last element, if autoScroll option is set.
      if (_options.autoScroll) {
        logRows.scrollTop = logRows.scrollHeight - logRows.clientHeight;
      }

      function SimpleStack() {
        var stack = "";
        var lines = Error().stack.split("\n");
        if (lines.length && lines.length > 0) {
          for (var i in lines) {
            var line = lines[i];
            if (line.indexOf("screenlog") == -1 && line.indexOf("jquery") == -1 && line.indexOf("SiteMaster_") == -1) {
              var atPos = line.indexOf("@");
              if (atPos >= 0) {
                var funcName = line.substring(0, atPos).replace(/[<\/@]/gi, "");
                var fileName = line.substring(atPos + 1).replace(/^.*\//gi, "").replace(/__.*(?=\.js)/i, "").replace(/:([0-9]+):[0-9]+/, " - line $1");
                line = fileName + " (" + funcName + ")";
              }
              //line = line.replace(/([a-z0-9_\/<.*\//gi, " - ");
              stack += line + "\n";
            }
          }
        }
        return stack;
      }
    };
  }

  function clear() {
    logRows.innerHTML = "";
    logEl.style.display = "none";
  }

  function serverlog() {
    return genericLogger(_options.logColor, "serverlog").apply(null, arguments);
  }

  function log() {
    return genericLogger(_options.logColor).apply(null, arguments);
  }

  function info() {
    return genericLogger(_options.infoColor).apply(null, arguments);
  }

  function warn() {
    return genericLogger(_options.warnColor).apply(null, arguments);
  }

  function error() {
    return genericLogger(_options.errorColor).apply(null, arguments);
  }

  function setOptions(options) {
    for (var i in options)
      if (options.hasOwnProperty(i) && _options.hasOwnProperty(i)) {
        _options[i] = options[i];
      }
  }

  function init(options) {
    if (isInitialized) {
      return;
    }

    isInitialized = true;

    if (options) {
      setOptions(options);
    }

    //logEl = createPanel();
    createPanel();
    document.body.appendChild(logEl);

    logEl.addEventListener("click", function (event) {
      if (event.target.className.indexOf("btnClear") > 0) {
        $(logEl).find(".chinchang-screenLog-rows").empty();
        $(logEl).fadeOut(50);
      }
    });

    if (!_options.freeConsole) {
      // Backup actual fns to keep it working together
      _console.log = console.log;
      _console.clear = console.clear;
      _console.info = console.info;
      _console.warn = console.warn;
      _console.error = console.error;
      console.log = originalFnCallDecorator(log, "log");
      console.clear = originalFnCallDecorator(clear, "clear");
      console.info = originalFnCallDecorator(info, "info");
      console.warn = originalFnCallDecorator(warn, "warn");
      console.error = originalFnCallDecorator(error, "error");
      console.serverlog = originalFnCallDecorator(serverlog, "log");
    }
  }

  function destroy() {
    isInitialized = false;
    console.log = _console.log;
    console.clear = _console.clear;
    console.info = _console.info;
    console.warn = _console.warn;
    console.error = _console.error;
    logEl.remove();
  }

  /**
   * Checking if isInitialized is set
   */
  function checkInitialized() {
    if (!isInitialized) {
      throw "You need to call `screenLog.init()` first.";
    }
  }

  /**
   * Decorator for checking if isInitialized is set
   * @param  {Function} fn Fn to decorate
   * @return {Function}      Decorated fn.
   */
  function checkInitDecorator(fn) {
    return function() {
      checkInitialized();
      return fn.apply(this, arguments);
    };
  }

  /**
   * Decorator for calling the original console's fn at the end of
   * our overridden fn definitions.
   * @param  {Function} fn Fn to decorate
   * @param  {string} fn Name of original function
   * @return {Function}      Decorated fn.
   */
  function originalFnCallDecorator(fn, fnName) {
    return function() {
      fn.apply(this, arguments);
      if (typeof _console[fnName] === "function") {
        _console[fnName].apply(console, arguments);
      }
    };
  }

  // Public API
  window.screenLog = {
    init: init,
    log: originalFnCallDecorator(checkInitDecorator(log), "log"),
    clear: originalFnCallDecorator(checkInitDecorator(clear), "clear"),
    info: originalFnCallDecorator(checkInitDecorator(clear), "info"),
    warn: originalFnCallDecorator(checkInitDecorator(warn), "warn"),
    error: originalFnCallDecorator(checkInitDecorator(error), "error"),
    destroy: checkInitDecorator(destroy)
  };
})();
