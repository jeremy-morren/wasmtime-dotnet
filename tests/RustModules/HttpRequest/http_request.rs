wit_bindgen::generate!({
    path: [],
    inline: r#"
        package wasmtime:http-request;

        interface api {
            /// Sends an outgoing HTTP request using the host's WASI HTTP implementation.
            ///
            /// The success payload is a JSON string with the shape:
            /// {
            ///   "status": number,
            ///   "headers": { "header-name": [[byte, ...], ...] },
            ///   "body": [byte, ...]
            /// }
            send-request: async func(method: string, url: string, body: option<list<u8>>) -> result<string, string>;
        }

        world http-request {
            export api;
        }
    "#,
});

use std::collections::BTreeMap;
use std::io::{Read, Write};

use exports::wasmtime::http_request::api::Guest;
use serde::Serialize;
use url::{Host, Url};
use wasi::http::outgoing_handler;
use wasi::http::types::{
    ErrorCode, Fields, HeaderError, IncomingBody, Method, OutgoingBody, OutgoingRequest, Scheme,
};

struct Component;

impl Guest for Component {
    async fn send_request(method: String, url: String, body: Option<Vec<u8>>) -> Result<String, String> {
        let request = PreparedRequest::new(&method, &url, body)?;
        let response = execute_request(&request).await?;
        serialize_response(&response)
    }
}

export!(Component);

struct PreparedRequest {
    method: Method,
    scheme: Scheme,
    authority: String,
    path_with_query: String,
    body: Option<Vec<u8>>,
}

struct CollectedResponse {
    status: u16,
    headers: Vec<(String, Vec<u8>)>,
    body: Vec<u8>,
}

#[derive(Serialize)]
struct JsonResponse {
    status: u16,
    headers: BTreeMap<String, Vec<Vec<u8>>>,
    body: Vec<u8>,
}

impl PreparedRequest {
    fn new(method: &str, url: &str, body: Option<Vec<u8>>) -> Result<Self, String> {
        let parsed_url = Url::parse(url).map_err(|error| format!("invalid URL '{url}': {error}"))?;

        if parsed_url.cannot_be_a_base() {
            return Err(format!("URL '{url}' cannot be used as an HTTP request target"));
        }

        let scheme = parse_scheme(&parsed_url)?;
        let authority = build_authority(&parsed_url)?;
        let path_with_query = build_path_with_query(&parsed_url);
        let method = parse_method(method)?;

        Ok(Self {
            method,
            scheme,
            authority,
            path_with_query,
            body,
        })
    }
}

async fn execute_request(request: &PreparedRequest) -> Result<CollectedResponse, String> {
    let headers = Fields::new();
    append_header(&headers, "accept", b"*/*")?;
    append_header(&headers, "user-agent", b"http-request.wasm/0.1")?;

    if let Some(body) = request.body.as_ref() {
        append_header(&headers, "content-length", body.len().to_string().as_bytes())?;
    }

    let outgoing_request = OutgoingRequest::new(headers);
    outgoing_request
        .set_method(&request.method)
        .map_err(|_| "failed to set the outgoing request method".to_string())?;
    outgoing_request
        .set_scheme(Some(&request.scheme))
        .map_err(|_| "failed to set the outgoing request scheme".to_string())?;
    outgoing_request
        .set_authority(Some(&request.authority))
        .map_err(|_| "failed to set the outgoing request authority".to_string())?;
    outgoing_request
        .set_path_with_query(Some(&request.path_with_query))
        .map_err(|_| "failed to set the outgoing request path and query".to_string())?;

    let outgoing_body = outgoing_request
        .body()
        .map_err(|_| "failed to get the outgoing request body handle".to_string())?;
    write_request_body(outgoing_body, request.body.as_deref())?;

    let future = outgoing_handler::handle(outgoing_request, None)
        .map_err(|error| format!("request was rejected before dispatch: {}", describe_error_code(&error)))?;

    let pollable = future.subscribe();
    let response = loop {
        if let Some(result) = future.get() {
            break result
                .map_err(|_| "response future was already consumed".to_string())?
                .map_err(|error| format!("request failed: {}", describe_error_code(&error)))?;
        }

        if !pollable.ready() {
            wit_bindgen::yield_async().await;
        }
    };

    let status = response.status();
    let headers = response.headers().entries();
    let body = read_response_body(response.consume().map_err(|_| {
        "failed to consume the incoming response body".to_string()
    })?)?;

    Ok(CollectedResponse {
        status,
        headers,
        body,
    })
}

fn write_request_body(outgoing_body: OutgoingBody, body: Option<&[u8]>) -> Result<(), String> {
    if let Some(bytes) = body {
        if !bytes.is_empty() {
            let mut writer = outgoing_body
                .write()
                .map_err(|_| "failed to get the outgoing body writer".to_string())?;
            writer
                .write_all(bytes)
                .map_err(|error| format!("failed to write the outgoing request body: {error}"))?;
            writer
                .flush()
                .map_err(|error| format!("failed to flush the outgoing request body: {error}"))?;
            drop(writer);
        }
    }

    OutgoingBody::finish(outgoing_body, None)
        .map_err(|error| format!("failed to finish the outgoing request body: {}", describe_error_code(&error)))
}

fn read_response_body(incoming_body: IncomingBody) -> Result<Vec<u8>, String> {
    let mut stream = incoming_body
        .stream()
        .map_err(|_| "failed to obtain the incoming response stream".to_string())?;
    let mut bytes = Vec::new();
    stream
        .read_to_end(&mut bytes)
        .map_err(|error| format!("failed to read the incoming response body: {error}"))?;
    drop(stream);
    let _future_trailers = IncomingBody::finish(incoming_body);
    Ok(bytes)
}

fn serialize_response(response: &CollectedResponse) -> Result<String, String> {
    let mut grouped_headers = BTreeMap::<String, Vec<Vec<u8>>>::new();
    for (name, value) in &response.headers {
        grouped_headers
            .entry(name.to_ascii_lowercase())
            .or_default()
            .push(value.clone());
    }

    serde_json::to_string(&JsonResponse {
        status: response.status,
        headers: grouped_headers,
        body: response.body.clone(),
    })
    .map_err(|error| format!("failed to serialize the JSON response: {error}"))
}

fn append_header(headers: &Fields, name: &str, value: &[u8]) -> Result<(), String> {
    let field_name = name.to_string();
    let field_value = value.to_vec();

    headers
        .append(&field_name, &field_value)
        .map_err(|error| format!("failed to append header '{name}': {}", describe_header_error(&error)))
}

fn parse_method(method: &str) -> Result<Method, String> {
    let trimmed = method.trim();
    if trimmed.is_empty() {
        return Err("HTTP method cannot be empty".to_string());
    }

    if !is_valid_http_token(trimmed) {
        return Err(format!("HTTP method '{trimmed}' contains invalid characters"));
    }

    Ok(match trimmed.to_ascii_uppercase().as_str() {
        "GET" => Method::Get,
        "HEAD" => Method::Head,
        "POST" => Method::Post,
        "PUT" => Method::Put,
        "DELETE" => Method::Delete,
        "CONNECT" => Method::Connect,
        "OPTIONS" => Method::Options,
        "TRACE" => Method::Trace,
        "PATCH" => Method::Patch,
        _ => Method::Other(trimmed.to_string()),
    })
}

fn parse_scheme(url: &Url) -> Result<Scheme, String> {
    match url.scheme() {
        "http" => Ok(Scheme::Http),
        "https" => Ok(Scheme::Https),
        scheme => Err(format!("unsupported URL scheme '{scheme}'; only http and https are supported")),
    }
}

fn build_authority(url: &Url) -> Result<String, String> {
    let host = match url.host() {
        Some(Host::Domain(domain)) => domain.to_string(),
        Some(Host::Ipv4(address)) => address.to_string(),
        Some(Host::Ipv6(address)) => format!("[{address}]"),
        None => return Err("URL must include a host".to_string()),
    };

    Ok(match url.port() {
        Some(port) => format!("{host}:{port}"),
        None => host,
    })
}

fn build_path_with_query(url: &Url) -> String {
    let path = if url.path().is_empty() { "/" } else { url.path() };
    match url.query() {
        Some(query) => format!("{path}?{query}"),
        None => path.to_string(),
    }
}

fn is_valid_http_token(value: &str) -> bool {
    value.bytes().all(|byte| matches!(
        byte,
        b'!' | b'#' | b'$' | b'%' | b'&' | b'\'' | b'*' | b'+' | b'-' | b'.' | b'^' | b'_' | b'`' | b'|' | b'~'
            | b'0'..=b'9' | b'A'..=b'Z' | b'a'..=b'z'
    ))
}

fn describe_header_error(error: &HeaderError) -> &'static str {
    match error {
        HeaderError::InvalidSyntax => "invalid syntax",
        HeaderError::Forbidden => "forbidden",
        HeaderError::Immutable => "immutable",
    }
}

fn describe_error_code(error: &ErrorCode) -> String {
    format!("{error:?}")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn it_builds_path_with_query_and_ignores_fragment() {
        let url = Url::parse("https://example.com/a/b?x=1#fragment").unwrap();

        let path_with_query = build_path_with_query(&url);

        assert_eq!(path_with_query, "/a/b?x=1");
    }

    #[test]
    fn it_wraps_ipv6_authorities() {
        let url = Url::parse("https://[::1]:8443/demo").unwrap();

        let authority = build_authority(&url).unwrap();

        assert_eq!(authority, "[::1]:8443");
    }

    #[test]
    fn it_parses_standard_and_extension_methods() {
        assert!(matches!(parse_method("get").unwrap(), Method::Get));
        assert!(matches!(parse_method("PuRgE").unwrap(), Method::Other(value) if value == "PuRgE"));
    }

    #[test]
    fn it_rejects_invalid_methods() {
        let error = parse_method("bad method").unwrap_err();

        assert!(error.contains("invalid characters"));
    }

    #[test]
    fn it_serializes_grouped_headers_and_body_as_json() {
        let response = CollectedResponse {
            status: 201,
            headers: vec![
                ("Set-Cookie".to_string(), b"a=1".to_vec()),
                ("set-cookie".to_string(), b"b=2".to_vec()),
                ("Content-Type".to_string(), b"text/plain".to_vec()),
            ],
            body: vec![1, 2, 3],
        };

        let json = serialize_response(&response).unwrap();

        assert_eq!(
            json,
            r#"{"status":201,"headers":{"content-type":[[116,101,120,116,47,112,108,97,105,110]],"set-cookie":[[97,61,49],[98,61,50]]},"body":[1,2,3]}"#
        );
    }
}