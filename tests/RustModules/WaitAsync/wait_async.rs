mod bindings {
	wit_bindgen::generate!({
		inline: r#"
			package wasmtime:wait-async;

			interface host {
				callback: async func(url: string) -> string;
			}

			interface api {
				wait-on-callback: func(url: string) -> string;
			}

			world wait-async {
				import host;
				export api;
			}
		"#,
		generate_all,
	});
}

use bindings::exports::wasmtime::wait_async::api::Guest;

struct WaitAsync;

impl Guest for WaitAsync {
	fn wait_on_callback(url: String) -> String {
		wit_bindgen::block_on(async { bindings::wasmtime::wait_async::host::callback(url).await })
	}
}

bindings::export!(WaitAsync with_types_in bindings);