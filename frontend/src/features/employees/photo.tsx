import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Camera, LoaderCircle } from 'lucide-react';
import { Avatar, Spinner } from '../../components/ui/feedback';
import { Button } from '../../components/ui/button';
import { Dialog } from '../../components/ui/overlays';
import { FormField } from '../../components/shared/form-field';
import { Input } from '../../components/ui/controls';
import { api } from '../../lib/api/client';
import { ApiError } from '../../lib/api/errors';

type Photo = {
  version: string | null;
  hasPhoto: boolean;
  width: number | null;
  height: number | null;
  sizeBytes: number | null;
  contentType: string | null;
};
const photoKey = (id: string) => ['employee-photo', id];
function usePhoto(id: string) {
  return useQuery({
    queryKey: photoKey(id),
    queryFn: ({ signal }) => api<Photo>(`/api/employees/${id}/photo`, { signal }),
    retry: false,
  });
}
function useObjectUrl(blob?: Blob | null) {
  const [resource, setResource] = useState<{ blob: Blob; url: string }>();
  useEffect(() => {
    if (!blob) return;
    const next = URL.createObjectURL(blob);
    let active = true;
    queueMicrotask(() => {
      if (active) setResource({ blob, url: next });
    });
    return () => {
      active = false;
      URL.revokeObjectURL(next);
    };
  }, [blob]);
  return resource?.blob === blob ? resource?.url : undefined;
}
export function EmployeeAvatar({ id, name }: { id: string; name: string }) {
  const metadata = usePhoto(id);
  const version = metadata.data?.version;
  const content = useQuery({
    queryKey: [...photoKey(id), 'content', version],
    queryFn: ({ signal }) =>
      api<Blob>(`/api/employees/${id}/photo/content?version=${version}`, {
        signal,
        response: 'blob',
      }),
    enabled: !!metadata.data?.hasPhoto && !!version,
    retry: false,
    gcTime: 0,
  });
  const url = useObjectUrl(metadata.data?.hasPhoto ? content.data : null);
  return (
    <span
      aria-busy={metadata.isPending || content.isFetching}
      className="relative inline-flex shrink-0 [&_img]:size-full [&_img]:object-cover"
    >
      <Avatar name={name} src={url} />
      {(metadata.isPending || content.isFetching) && (
        <span
          role="status"
          aria-label="Loading photograph"
          className="absolute bottom-0 right-0 rounded-full bg-surface p-0.5"
        >
          <LoaderCircle aria-hidden="true" className="size-3 animate-spin" />
        </span>
      )}
    </span>
  );
}
function photoError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 413) return 'Maximum photo size is 5 MiB.';
    if (error.status === 415 || error.status === 400)
      return 'Choose a complete JPEG or PNG image, no larger than 5 MiB or 4096 × 4096 pixels.';
    return error.message;
  }
  return 'Unable to save the photograph. Try again.';
}
export function EmployeePhotoControl({
  id,
  name,
  manage,
}: {
  id: string;
  name: string;
  manage: boolean;
}) {
  const photo = usePhoto(id);
  const [open, setOpen] = useState(false);
  const [notice, setNotice] = useState('');
  const opener = useRef<HTMLButtonElement>(null);
  return (
    <div className="space-y-2">
      {manage && (
        <Button
          ref={opener}
          variant="outline"
          density="compact"
          type="button"
          onClick={() => setOpen(true)}
        >
          <Camera aria-hidden="true" className="size-4" />
          Manage photo
        </Button>
      )}
      {notice && (
        <p role="status" className="text-sm">
          {notice}
        </p>
      )}
      {open && (
        <PhotoEditor
          id={id}
          name={name}
          photo={photo.data}
          error={photo.error}
          loading={photo.isPending}
          retry={() => void photo.refetch()}
          returnFocus={() => opener.current?.focus()}
          close={() => setOpen(false)}
          saved={(message) => {
            setNotice(message);
            setOpen(false);
          }}
        />
      )}
    </div>
  );
}
function PhotoEditor({
  id,
  name,
  photo,
  error,
  loading,
  retry,
  close,
  saved,
  returnFocus,
}: {
  id: string;
  name: string;
  photo?: Photo;
  error: Error | null;
  loading: boolean;
  retry: () => void;
  close: () => void;
  saved: (message: string) => void;
  returnFocus: () => void;
}) {
  const cache = useQueryClient();
  const [file, setFile] = useState<File>();
  const [validation, setValidation] = useState('');
  const [remove, setRemove] = useState(false);
  const submitting = useRef(false);
  const preview = useObjectUrl(file);
  useEffect(() => {
    const prevent = (event: BeforeUnloadEvent) => {
      if (file || submitting.current) {
        event.preventDefault();
        event.returnValue = '';
      }
    };
    window.addEventListener('beforeunload', prevent);
    return () => window.removeEventListener('beforeunload', prevent);
  }, [file]);
  const mutation = useMutation({
    mutationFn: async (removal: boolean) => {
      if (removal)
        return api<Photo>(`/api/employees/${id}/photo`, {
          method: 'DELETE',
          body: { expectedVersion: photo?.version },
        });
      const form = new FormData();
      form.append('file', file!);
      if (photo?.version) form.append('expectedVersion', photo.version);
      return api<Photo>(`/api/employees/${id}/photo`, { method: 'PUT', body: form });
    },
    onSuccess: async (value, removal) => {
      cache.removeQueries({ queryKey: [...photoKey(id), 'content'] });
      cache.setQueryData(photoKey(id), value);
      await cache.invalidateQueries({ queryKey: photoKey(id) });
      saved(removal ? 'Profile photograph removed.' : 'Profile photograph saved.');
    },
    onError: (failure) => {
      setValidation(photoError(failure));
      if (failure instanceof ApiError && failure.status === 409) retry();
    },
    onSettled: () => {
      submitting.current = false;
    },
  });
  function command(removal: boolean) {
    if (submitting.current || !photo || (!removal && !file)) return;
    submitting.current = true;
    setValidation('');
    mutation.mutate(removal);
  }
  return (
    <Dialog
      open
      size="md"
      title="Employee photograph"
      description={`${name}. Photographs are stored privately and viewed only through authorized employee access.`}
      dirty={!!file || remove}
      pending={mutation.isPending}
      onCloseAutoFocus={(event) => {
        event.preventDefault();
        returnFocus();
      }}
      onOpenChange={(value) => {
        if (!value) close();
      }}
      footer={
        <>
          <Button
            type="button"
            loading={mutation.isPending}
            disabled={!photo || !file || remove}
            onClick={() => command(false)}
          >
            {photo?.hasPhoto ? 'Replace photo' : 'Upload photo'}
          </Button>
          {photo?.hasPhoto && (
            <Button
              type="button"
              variant="destructive-outline"
              disabled={mutation.isPending}
              onClick={() => setRemove(true)}
            >
              Remove photo
            </Button>
          )}
        </>
      }
    >
      {loading ? (
        <Spinner label="Loading photograph information" />
      ) : error ? (
        <div role="alert">
          <p>Photo management is unavailable. {photoError(error)}</p>
          <Button type="button" variant="outline" onClick={retry}>
            Try again
          </Button>
        </div>
      ) : (
        <div className="space-y-4">
          <div className="flex items-center gap-3">
            <EmployeeAvatar id={id} name={name} />
            <span className="text-sm">
              {photo?.hasPhoto ? 'Current photograph' : 'No photograph. Initials are displayed.'}
            </span>
          </div>
          <FormField
            id="employee-photo-file"
            label="JPEG or PNG photograph"
            hint="Maximum 5 MiB. Maximum decoded dimensions 4096 × 4096 pixels."
            error={validation || undefined}
          >
            {(props) => (
              <Input
                {...props}
                type="file"
                accept="image/jpeg,image/png"
                disabled={mutation.isPending}
                onChange={(event) => {
                  const next = event.target.files?.[0];
                  setRemove(false);
                  setValidation('');
                  setFile(undefined);
                  if (!next) return;
                  if (next.size > 5 * 1024 * 1024) {
                    setValidation('Maximum photo size is 5 MiB.');
                    return;
                  }
                  if (!['image/jpeg', 'image/png'].includes(next.type)) {
                    setValidation('Choose a JPEG or PNG photograph.');
                    return;
                  }
                  setFile(next);
                }}
              />
            )}
          </FormField>
          {preview && (
            <img
              src={preview}
              alt="Selected photograph preview"
              className="max-h-48 max-w-full rounded-sm object-contain"
              onError={() => {
                setValidation(
                  'This photograph could not be previewed. Choose a valid JPEG or PNG.',
                );
                setFile(undefined);
              }}
            />
          )}
          {mutation.isPending && <Spinner label="Saving photograph" />}
          {remove && (
            <section className="space-y-2" aria-label="Confirm photograph removal">
              <p className="text-sm">
                Remove this photograph from the profile? Initials will be displayed. Its historical
                audit revision is retained privately.
              </p>
              <Button
                variant="destructive"
                type="button"
                disabled={mutation.isPending}
                onClick={() => command(true)}
              >
                Confirm removal
              </Button>
              <Button
                type="button"
                variant="outline"
                disabled={mutation.isPending}
                onClick={() => setRemove(false)}
              >
                Keep photograph
              </Button>
            </section>
          )}
        </div>
      )}
    </Dialog>
  );
}
